using System;
using System.Linq;
using Naninovel;
using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor
{
    /// <summary>
    /// Вкладка «Связи»: список переменных карты и карточка выбранной — объявление, область, текущее значение (Play Mode),
    /// связи в сценариях <c>.nani</c> и в квестах Vortex отдельными секциями. Клик по месту — переход к нему.
    /// </summary>
    internal sealed partial class CustomVariablesDashboardWindow
    {
        private const float ListWidth = 320f;
        private const float OperationWidth = 80f;
        private const float ValueWidth = 160f;
        private const float FindButtonWidth = 40f;

        /// <summary>Отступы строки связи и полоса прокрутки карточки — то, что не занято колонками</summary>
        private const float LinkRowChrome = 40f;

        /// <summary>Минимум видимых символов пути места, даже если ширины окна не хватает</summary>
        private const int MinLocationChars = 20;

        private const string Ellipsis = "…";

        private static readonly Color LossColor = new(0.9f, 0.45f, 0.2f, 0.18f);
        private static readonly Color SelectionColor = new(0.24f, 0.48f, 0.9f, 0.35f);

        private string _linksFilter = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _cardScroll;

        private void DrawLinks()
        {
            var index = DrawBuildBar();
            if (index == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawVariableList(index);
                DrawCard(index);
            }
        }

        private void DrawVariableList(VariableIndex index)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                _linksFilter = EditorGUILayout.TextField(_linksFilter, EditorStyles.toolbarSearchField);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                var entries = index.Entries.Values
                    .Where(e => _linksFilter.Length == 0 ||
                                e.Name.IndexOf(_linksFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.Name, StringComparer.Ordinal);
                foreach (var entry in entries)
                    DrawListRow(index, entry);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawListRow(VariableIndex index, VariableEntry entry)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var losses = LossAnalysis.GetVisible(entry);
            if (entry.Name == selected)
                EditorGUI.DrawRect(rect, SelectionColor);
            else if (losses != LossKinds.None)
                EditorGUI.DrawRect(rect, LossColor);

            var counts = index.QuestsEnabled
                ? $"{entry.CountOf(LinkSource.Nani)} / {entry.CountOf(LinkSource.Quest)}"
                : $"{entry.CountOf(LinkSource.Nani)}";
            var name = entry.Declaration == null ? $"{entry.Name}  (не объявлена)" : entry.Name;
            var tooltip = losses == LossKinds.None ? null : DescribeLosses(losses);
            if (GUI.Button(new Rect(rect.x, rect.y, rect.width - 60f, rect.height), new GUIContent(name, tooltip),
                    EditorStyles.label))
                Select(entry.Name);
            EditorGUI.LabelField(new Rect(rect.xMax - 58f, rect.y, 58f, rect.height), counts, EditorStyles.miniLabel);
        }

        private void DrawCard(VariableIndex index)
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_cardScroll);
            _cardScroll = scroll.scrollPosition;

            if (selected == null || !index.Entries.TryGetValue(selected, out var entry))
            {
                EditorGUILayout.HelpBox("Выберите переменную в списке.", MessageType.None);
                return;
            }

            GUILayout.Label(entry.Name, EditorStyles.boldLabel);
            var scope = CustomVariablesConfiguration.HasGlobalPrefix(entry.Name) ? "Global" : "Local";
            GUILayout.Label(entry.Declaration == null
                ? $"{scope} · не объявлена в конфиге"
                : $"{scope} · объявлена, начальное значение: {entry.Declaration.InitialValue}", EditorStyles.miniLabel);

            var manager = GetManager();
            if (manager != null)
                GUILayout.Label(manager.VariableExists(entry.Name)
                    ? $"Текущее значение: {Format(manager.GetVariableValue(entry.Name))}"
                    : "Текущее значение: переменной нет в рантайме", EditorStyles.miniLabel);

            var losses = LossAnalysis.GetVisible(entry);
            if (losses != LossKinds.None)
                EditorGUILayout.HelpBox(DescribeLosses(losses), MessageType.Warning);

            var twins = index.Entries.Keys
                .Where(n => n != entry.Name && n.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (twins.Count > 0)
                EditorGUILayout.HelpBox($"Двойники по регистру: {string.Join(", ", twins)}", MessageType.Warning);

            // Ширина под путь места: окно минус список, колонки строки и отступы
            var locationWidth = position.width - ListWidth - OperationWidth - ValueWidth - FindButtonWidth - LinkRowChrome;
            DrawLinkSection("Сценарии .nani", entry, LinkSource.Nani, null, locationWidth);
            DrawLinkSection("Квесты Vortex", entry, LinkSource.Quest,
                index.QuestsEnabled ? null : "Квесты Vortex отключены (USING_VORTEX_QUESTS).", locationWidth);
        }

        private static void DrawLinkSection(string title, VariableEntry entry, LinkSource source, string disabledText,
            float locationWidth)
        {
            EditorGUILayout.Space(6f);
            var links = entry.Links.Where(l => l.Source == source).ToList();
            GUILayout.Label(disabledText == null ? $"{title} ({links.Count})" : title, EditorStyles.boldLabel);
            if (disabledText != null)
            {
                GUILayout.Label(disabledText, EditorStyles.miniLabel);
                return;
            }

            if (links.Count == 0)
            {
                GUILayout.Label("Связей нет", EditorStyles.miniLabel);
                return;
            }

            foreach (var link in links)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(OperationTitle(link.Operation), GUILayout.Width(OperationWidth));
                    GUILayout.Label(new GUIContent(DescribeValue(link), link.RawText), GUILayout.Width(ValueWidth));
                    // Find — ассет места в инспекторе; ссылка — переход к месту (сценарий — во внешнем редакторе на строке)
                    if (GUILayout.Button(new GUIContent("Find", "Выделить ассет и показать в инспекторе"),
                            EditorStyles.miniButton, GUILayout.Width(FindButtonWidth)))
                        link.Location.Find();
                    var location = link.Location.Describe();
                    var shown = FitFromEnd(location, locationWidth, EditorStyles.linkLabel);
                    if (GUILayout.Button(new GUIContent(shown, location), EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                        link.Location.Open();
                }
        }

        /// <summary>
        /// Текст, укороченный с начала под ширину: «…/Act1/BlazeDate01.nani:3» — конец пути (файл и строка) важнее.
        /// Видимых символов не меньше <see cref="MinLocationChars"/>, даже если ширины не хватает.
        /// </summary>
        private static string FitFromEnd(string text, float width, GUIStyle style)
        {
            if (text.Length <= MinLocationChars || style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            // Наибольший хвост, который влезает вместе с многоточием; бинарный поиск по длине хвоста
            int low = MinLocationChars, high = text.Length - 1;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (style.CalcSize(new GUIContent(Ellipsis + text.Substring(text.Length - mid))).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return Ellipsis + text.Substring(text.Length - low);
        }

        /// <summary>Оператор и операнд связи: «++», «&gt;= 3», «= Day+1»; у чтения и прочего — исходный текст</summary>
        private static string DescribeValue(VariableLink link)
        {
            if (link.Operation is NaniVariableOperation.Read or NaniVariableOperation.Other)
                return link.RawText;
            return $"{link.OperatorText} {link.Operand}".Trim();
        }

        private static string OperationTitle(NaniVariableOperation operation) => operation switch
        {
            NaniVariableOperation.Write => "запись",
            NaniVariableOperation.Compare => "сравнение",
            NaniVariableOperation.Wait => "ожидание",
            NaniVariableOperation.Read => "чтение",
            _ => "прочее"
        };

        private static string DescribeLosses(LossKinds losses) =>
            string.Join("; ", LossAnalysis.Kinds.Where(k => (losses & k) != 0).Select(LossAnalysis.Title));
    }
}
