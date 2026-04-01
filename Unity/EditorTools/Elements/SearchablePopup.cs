#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.EditorSettings;

namespace Vortex.Unity.EditorTools.Elements
{
    /// <summary>
    /// Универсальный пикер (EditorWindow popup) с поддержкой:
    /// — поиска по маске (автоматически при > SearchThreshold элементов)
    /// — группировки по "/" (раскрываемые папки, по умолчанию свёрнуты)
    /// — Dictionary / string[] на входе
    /// </summary>
    public static class SearchablePopup
    {
        private const string DefaultPlaceholder = "--[Choose]--";
        private static readonly Dictionary<int, int> SelectionCache = new();

        public static int Draw(Rect rect, int controlId, int selectedIndex, string[] items,
            string placeholder = null)
        {
            placeholder ??= DefaultPlaceholder;

            if (!SelectionCache.ContainsKey(controlId))
                SelectionCache[controlId] = selectedIndex;
            else if (SelectionCache[controlId] != selectedIndex && selectedIndex >= 0)
                SelectionCache[controlId] = selectedIndex;

            var cachedIndex = SelectionCache[controlId];
            var displayText = cachedIndex >= 0 && cachedIndex < items.Length
                ? items[cachedIndex]
                : placeholder;

            if (GUI.Button(rect, new GUIContent(displayText), EditorStyles.popup))
            {
                var min = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
                var screenRect = new Rect(min.x, min.y, rect.width, rect.height);
                SearchablePopupWindow.Show(screenRect, items, placeholder, cachedIndex,
                    index => { SelectionCache[controlId] = index; });
            }

            return SelectionCache[controlId];
        }

        public static string Draw<T>(Rect rect, int controlId, string selectedKey,
            Dictionary<string, T> dictionary, string placeholder = null)
        {
            var keys = dictionary.Keys.ToArray();
            var currentIndex = string.IsNullOrEmpty(selectedKey) ? -1 : Array.IndexOf(keys, selectedKey);
            var newIndex = Draw(rect, controlId, currentIndex, keys, placeholder);
            return newIndex >= 0 && newIndex < keys.Length ? keys[newIndex] : null;
        }

        public static void ResetSelection(int controlId) => SelectionCache.Remove(controlId);
    }

    // ════════════════════════════════════════════════════════════
    //  EditorWindow popup
    // ════════════════════════════════════════════════════════════

    internal class SearchablePopupWindow : EditorWindow
    {
        private const int SearchThreshold = 10;
        private const float RowHeight = 20f;
        private const float GroupIndent = 14f;
        private const float FoldoutArrowWidth = 14f;
        private const float Pad = 4f;
        private const float MaxHeight = 300f;
        private const float SearchFieldHeight = 20f;
        private const float MinWidth = 180f;

        private string[] _items;
        private string _placeholder;
        private int _selectedIndex;
        private Action<int> _onSelected;

        private string _searchFilter = "";
        private Vector2 _scroll;
        private HashSet<string> _collapsedGroups = new();
        private bool _hasGroups;
        private bool _showSearch;
        private bool _needFocusSearch;

        // Плоский список строк для отрисовки (группы + элементы, отсортированные)
        private List<RowData> _allRows;

        private enum RowType
        {
            Placeholder,
            GroupHeader,
            Item
        }

        private struct RowData
        {
            public RowType Type;
            public string Label; // текст для отображения
            public string GroupPath; // полный путь группы (для GroupHeader — сам путь, для Item — путь родителя)
            public int ItemIndex; // индекс в _items (-1 для placeholder и групп)
            public int Depth; // уровень вложенности (0 = корень)
        }

        private static SearchablePopupWindow currentInstance;

        private void OnEnable()
        {
            currentInstance = this;
        }

        private void OnDisable()
        {
            if (currentInstance == this)
                currentInstance = null;
        }

        public static void CloseAll()
        {
            if (currentInstance != null)
            {
                currentInstance.Close();
                currentInstance = null;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Show
        // ════════════════════════════════════════════════════════

        public static void Show(Rect buttonScreenRect, string[] items, string placeholder, int selectedIndex,
            Action<int> onSelected)
        {
            var windows = Resources.FindObjectsOfTypeAll<SearchablePopupWindow>().ToArray();

            foreach (var w in windows)
            {
                // Многоуровневая проверка + защита от исключений
                try
                {
                    if (w == null) continue;

                    if (w.GetInstanceID() == 0) continue;

                    try
                    {
                        _ = w.position;
                    }
                    catch (NullReferenceException)
                    {
                        continue;
                    }

                    w.Close();
                }
                catch (NullReferenceException)
                {
                }
                catch (UnityException)
                {
                }
            }

            var win = CreateInstance<SearchablePopupWindow>();
            win._items = items;
            win._placeholder = placeholder;
            win._selectedIndex = selectedIndex;
            win._onSelected = onSelected;
            win.Build();

            var width = Mathf.Max(buttonScreenRect.width, MinWidth);
            // Высота — по общему кол-ву элементов (items + placeholder), не по видимым группам
            var totalRows = items.Length + (placeholder != null ? 1 : 0); // +1 placeholder
            var h = totalRows * RowHeight + Pad * 2f;
            if (win._showSearch) h += SearchFieldHeight + Pad;
            h = Mathf.Min(h, MaxHeight);

            win.ShowAsDropDown(buttonScreenRect, new Vector2(width, h));
            win._needFocusSearch = true;
        }

        // ════════════════════════════════════════════════════════
        //  Построение плоского списка
        // ════════════════════════════════════════════════════════

        private void Build()
        {
            _showSearch = _items.Length > SearchThreshold;

            // Парсим entries
            var entries = new List<(string displayName, string shortName, string groupPath, int itemIndex)>();
            for (int i = 0; i < _items.Length; i++)
            {
                var n = _items[i] ?? "";
                var slash = n.LastIndexOf('/');
                entries.Add((
                    n,
                    slash >= 0 ? n.Substring(slash + 1) : n,
                    slash >= 0 ? n.Substring(0, slash) : "",
                    i
                ));
            }

            _hasGroups = entries.Any(e => !string.IsNullOrEmpty(e.groupPath));

            entries.Sort((a, b) =>
            {
                // Первичная сортировка: по groupPath (строковое сравнение)
                var cmp = string.Compare(a.groupPath, b.groupPath, StringComparison.Ordinal);

                // Вторичная сортировка: по itemIndex (числовое сравнение)
                return cmp != 0 ? cmp : a.itemIndex.CompareTo(b.itemIndex);
            });

            // Строим плоский список строк: placeholder → [группа → дети]*
            _allRows = new List<RowData>();
            if (_placeholder != null)
                _allRows.Add(new RowData
                    {
                        Type = RowType.Placeholder,
                        Label = _placeholder,
                        GroupPath = "",
                        ItemIndex = -1,
                        Depth = 0
                    }
                );

            var insertedGroups = new HashSet<string>();

            foreach (var e in entries)
            {
                if (_hasGroups && !string.IsNullOrEmpty(e.groupPath))
                {
                    // Вставляем заголовки групп по сегментам (если ещё не вставлены)
                    var segs = e.groupPath.Split('/');
                    var path = "";
                    for (int s = 0; s < segs.Length; s++)
                    {
                        path = s == 0 ? segs[s] : path + "/" + segs[s];
                        if (insertedGroups.Add(path))
                        {
                            _allRows.Add(new RowData
                            {
                                Type = RowType.GroupHeader,
                                Label = segs[s],
                                GroupPath = path,
                                ItemIndex = -1,
                                Depth = s
                            });
                        }
                    }

                    // Элемент
                    _allRows.Add(new RowData
                    {
                        Type = RowType.Item,
                        Label = e.shortName,
                        GroupPath = e.groupPath,
                        ItemIndex = e.itemIndex,
                        Depth = segs.Length
                    });
                }
                else
                {
                    _allRows.Add(new RowData
                    {
                        Type = RowType.Item,
                        Label = e.shortName,
                        GroupPath = "",
                        ItemIndex = e.itemIndex,
                        Depth = 0
                    });
                }
            }

            // Все группы свёрнуты по умолчанию
            if (_hasGroups)
            {
                foreach (var row in _allRows)
                    if (row.Type == RowType.GroupHeader)
                        _collapsedGroups.Add(row.GroupPath);
            }
        }

        // ════════════════════════════════════════════════════════
        //  Фильтрация
        // ════════════════════════════════════════════════════════

        private List<RowData> GetVisibleRows()
        {
            var filtered = string.IsNullOrEmpty(_searchFilter)
                ? _allRows
                : FilterBySearch(_searchFilter);

            var result = new List<RowData>();
            foreach (var row in filtered)
            {
                if (row.Type == RowType.Placeholder)
                {
                    result.Add(row);
                    continue;
                }

                if (row.Type == RowType.GroupHeader)
                {
                    // Показываем заголовок группы, если он не скрыт свёрнутым родителем
                    if (!IsParentCollapsed(row.GroupPath))
                        result.Add(row);
                    continue;
                }

                // Item — показываем если родительская группа не свёрнута
                if (string.IsNullOrEmpty(row.GroupPath) || !IsAnyAncestorCollapsed(row.GroupPath))
                    result.Add(row);
            }

            return result;
        }

        private List<RowData> FilterBySearch(string filter)
        {
            var lo = filter.ToLowerInvariant();
            // Находим ItemIndex'ы совпавших элементов и их группы
            var matchedGroups = new HashSet<string>();
            var result = new List<RowData>();

            foreach (var row in _allRows)
            {
                if (row.Type == RowType.Placeholder)
                {
                    result.Add(row);
                    continue;
                }

                if (row.Type == RowType.Item)
                {
                    var displayName = row.ItemIndex >= 0 && row.ItemIndex < _items.Length
                        ? _items[row.ItemIndex]
                        : row.Label;
                    if (displayName.ToLowerInvariant().Contains(lo) ||
                        row.Label.ToLowerInvariant().Contains(lo))
                    {
                        result.Add(row);
                        // Собираем все родительские группы
                        if (!string.IsNullOrEmpty(row.GroupPath))
                        {
                            var segs = row.GroupPath.Split('/');
                            var p = "";
                            for (int i = 0; i < segs.Length; i++)
                            {
                                p = i == 0 ? segs[i] : p + "/" + segs[i];
                                matchedGroups.Add(p);
                            }
                        }
                    }
                }
            }

            // Добавляем заголовки групп, нужные для найденных элементов
            var withGroups = new List<RowData>();
            var addedGroups = new HashSet<string>();

            foreach (var row in result)
            {
                if (row.Type == RowType.Placeholder)
                {
                    withGroups.Add(row);
                    continue;
                }

                // Перед item — вставляем его группы
                if (row.Type == RowType.Item && !string.IsNullOrEmpty(row.GroupPath))
                {
                    var segs = row.GroupPath.Split('/');
                    var p = "";
                    for (int i = 0; i < segs.Length; i++)
                    {
                        p = i == 0 ? segs[i] : p + "/" + segs[i];
                        if (addedGroups.Add(p))
                        {
                            // Найти GroupHeader row
                            var gh = _allRows.FirstOrDefault(r => r.Type == RowType.GroupHeader && r.GroupPath == p);
                            if (gh.Type == RowType.GroupHeader)
                                withGroups.Add(gh);
                        }
                    }
                }

                withGroups.Add(row);
            }

            return withGroups;
        }

        /// <summary>
        /// Проверяет, свёрнут ли непосредственный родитель группы.
        /// Для "A/B" проверяет "A".
        /// </summary>
        private bool IsParentCollapsed(string groupPath)
        {
            var lastSlash = groupPath.LastIndexOf('/');
            if (lastSlash < 0) return false; // корневая группа — родителя нет
            var parent = groupPath.Substring(0, lastSlash);
            return _collapsedGroups.Contains(parent) || IsParentCollapsed(parent);
        }

        /// <summary>
        /// Проверяет, свёрнут ли ЛЮБОЙ предок (включая непосредственную группу).
        /// </summary>
        private bool IsAnyAncestorCollapsed(string groupPath)
        {
            var segs = groupPath.Split('/');
            var p = "";
            for (int i = 0; i < segs.Length; i++)
            {
                p = i == 0 ? segs[i] : p + "/" + segs[i];
                if (_collapsedGroups.Contains(p)) return true;
            }

            return false;
        }

        // ════════════════════════════════════════════════════════
        //  OnGUI
        // ════════════════════════════════════════════════════════

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                return;
            }

            var area = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(area, ToolsSettings.GetBgColor(DefaultColors.BoxBg));
            DrawBorder(area, ToolsSettings.GetLineColor(DefaultColors.BorderColor));

            float y = Pad;

            // ── Поиск ──
            if (_showSearch)
            {
                var sr = new Rect(Pad, y, area.width - Pad * 2f, SearchFieldHeight);
                GUI.SetNextControlName("_popup_search_");
                var nf = EditorGUI.TextField(sr, _searchFilter, EditorStyles.toolbarSearchField);
                if (nf != _searchFilter)
                {
                    _searchFilter = nf;
                    _scroll = Vector2.zero;

                    // При поиске — раскрываем все группы с найденными элементами
                    if (!string.IsNullOrEmpty(_searchFilter) && _hasGroups)
                        ExpandAllGroups();
                    else if (string.IsNullOrEmpty(_searchFilter) && _hasGroups)
                        CollapseAllGroups();
                }

                if (_needFocusSearch)
                {
                    EditorGUI.FocusTextInControl("_popup_search_");
                    _needFocusSearch = false;
                }

                y += SearchFieldHeight + Pad;
            }

            // ── Список ──
            var visible = GetVisibleRows();
            var svRect = new Rect(Pad, y, area.width - Pad * 2f, area.height - y - Pad);
            var contentHeight = visible.Count * RowHeight;
            var cRect = new Rect(0, 0, svRect.width - (contentHeight > svRect.height ? 14f : 0f), contentHeight);

            _scroll = GUI.BeginScrollView(svRect, _scroll, cRect);

            for (int i = 0; i < visible.Count; i++)
            {
                var row = visible[i];
                var rowRect = new Rect(row.Depth * GroupIndent, i * RowHeight,
                    cRect.width - row.Depth * GroupIndent, RowHeight);

                switch (row.Type)
                {
                    case RowType.Placeholder:
                        if (DrawItemRow(rowRect, row.Label, _selectedIndex == -1))
                            Pick(-1);
                        break;

                    case RowType.GroupHeader:
                        var collapsed = _collapsedGroups.Contains(row.GroupPath);
                        if (DrawGroupRow(rowRect, row.Label, collapsed))
                        {
                            if (collapsed) _collapsedGroups.Remove(row.GroupPath);
                            else _collapsedGroups.Add(row.GroupPath);
                            Repaint();
                        }

                        break;

                    case RowType.Item:
                        if (DrawItemRow(rowRect, row.Label, row.ItemIndex == _selectedIndex))
                            Pick(row.ItemIndex);
                        break;
                }
            }

            GUI.EndScrollView();
        }

        // ════════════════════════════════════════════════════════
        //  Выбор
        // ════════════════════════════════════════════════════════

        private void Pick(int itemIndex)
        {
            _selectedIndex = itemIndex;
            _onSelected?.Invoke(itemIndex);
            Close();
        }

        // ════════════════════════════════════════════════════════
        //  Отрисовка строк
        // ════════════════════════════════════════════════════════

        private bool DrawItemRow(Rect rect, string text, bool selected)
        {
            if (selected)
            {
                var c = ToolsSettings.GetBgColor(DefaultColors.BadgeBg);
                c.a = 0.6f;
                EditorGUI.DrawRect(rect, c);
            }

            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
            {
                var c = ToolsSettings.GetBgColor(DefaultColors.HeaderBgCollapsed);
                c.a = 0.25f;
                EditorGUI.DrawRect(rect, c);
            }

            var lr = new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height);
            GUI.Label(lr, text, selected
                ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold }
                : EditorStyles.label);

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private bool DrawGroupRow(Rect rect, string name, bool collapsed)
        {
            var bg = ToolsSettings.GetBgColor(DefaultColors.HeaderBgCollapsed);
            bg.a = 0.4f;
            EditorGUI.DrawRect(rect, bg);

            var ar = new Rect(rect.x + 2f, rect.y, FoldoutArrowWidth, rect.height);
            GUI.Label(ar, collapsed ? "▶" : "▼", new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive) }
            });

            var lr = new Rect(rect.x + FoldoutArrowWidth + 2f, rect.y,
                rect.width - FoldoutArrowWidth - 4f, rect.height);
            GUI.Label(lr, name, EditorStyles.boldLabel);

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        // ════════════════════════════════════════════════════════
        //  Свёртка / развёртка
        // ════════════════════════════════════════════════════════

        private void ExpandAllGroups() => _collapsedGroups.Clear();

        private void CollapseAllGroups()
        {
            _collapsedGroups.Clear();
            foreach (var row in _allRows)
                if (row.Type == RowType.GroupHeader)
                    _collapsedGroups.Add(row.GroupPath);
        }

        // ════════════════════════════════════════════════════════

        private static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
#endif