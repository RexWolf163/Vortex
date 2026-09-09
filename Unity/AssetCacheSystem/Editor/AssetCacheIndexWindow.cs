#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vortex.Unity.AssetCacheSystem.Bus;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Editor
{
    /// <summary>
    /// Runtime-инспектор индекса AssetCacheSystem: <c>Tools/Vortex/AssetsCache/Runtime Index</c>.
    ///
    /// Показывает текущее заполнение реестра (<c>Handles</c> / <c>Inflight</c> / <c>Survivors</c>) и для
    /// каждого <see cref="AssetReference"/> — количество удерживающих его владельцев (ссылок), то есть
    /// ровно то, по чему контроллер решает, уходит ли ассет в survivors и выгружается ли он по LRU.
    ///
    /// Окно живёт в редакторной части рантайм-сборки пакета, поэтому читает <c>internal</c>-словари
    /// модели напрямую: публичного реактивного API у модели нет (см. <c>AssetCacheModel</c>) и заводить
    /// его ради диагностики не нужно. Окно только читает — никаких Release/Evict отсюда не делается,
    /// чтобы наблюдение не меняло наблюдаемое.
    ///
    /// Данные есть только в Play Mode: до bootstrap'а модель не создана.
    /// </summary>
    public class AssetCacheIndexWindow : EditorWindow
    {
        /// <summary>Положение ассета в реестре. Порядок членов = порядок группировки в таблице.</summary>
        private enum RefState
        {
            /// <summary>Идёт <c>Addressables.LoadAssetAsync</c>, handle ещё не в <c>Handles</c>.</summary>
            Inflight,

            /// <summary>Загружен и удерживается владельцами.</summary>
            Active,

            /// <summary>Загружен, владельцев нет, ждёт eviction в LRU-очереди.</summary>
            Survivor
        }

        private sealed class Row
        {
            public string Guid;
            public string Name;
            public string TypeName;
            public RefState State;
            public int Refs;

            /// <summary>Позиция в очереди survivors, 1 = голова (выгрузится первой). 0 — не survivor.</summary>
            public int LruPosition;

            public readonly List<string> Owners = new();
        }

        private const float StateWidth = 72f;
        private const float RefsWidth = 46f;
        private const float TypeWidth = 120f;
        private const float LruWidth = 58f;

        private readonly List<Row> _rows = new();
        private readonly HashSet<string> _expanded = new();

        private Vector2 _scroll;
        private string _filter = "";
        private bool _autoRefresh = true;

        private int _loadedCount;
        private int _activeCount;
        private int _survivorCount;
        private int _inflightCount;
        private int _ownerCount;
        private int _deadOwnerCount;
        private int _capacity;

        public static void Open()
        {
            var window = GetWindow<AssetCacheIndexWindow>("AssetCache Index");
            window.minSize = new Vector2(520f, 220f);
            window.Rebuild();
        }

        private void OnEnable() => Rebuild();

        /// <summary>
        /// Вызывается редактором ~10 раз в секунду для видимого окна — этого хватает, чтобы видеть
        /// inflight-загрузки и движение survivors, и при этом не пересобирать снимок в каждом
        /// OnGUI-событии (их на кадр несколько: Layout + Repaint).
        /// </summary>
        private void OnInspectorUpdate()
        {
            if (!_autoRefresh) return;
            Rebuild();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!AssetCache.IsReady || AssetCache.Data == null)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "AssetCache не инициализирован: bootstrap ждёт Settings.OnInit."
                        : "Индекс существует только в Play Mode — запусти игру.",
                    MessageType.Info);
                return;
            }

            DrawSummary();
            DrawTable();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Авто", EditorStyles.toolbarButton,
                    GUILayout.Width(50f));

                if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    Rebuild();

                // Отсюда правится SurvivorCapacity — главная настройка, влияющая на то, что видно в окне.
                if (GUILayout.Button("Конфиг", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    MenuController.FindConfig();

                GUILayout.FlexibleSpace();
                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
            }
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField(
                $"Загружено: {_loadedCount}  (active {_activeCount} / survivors {_survivorCount})   " +
                $"Inflight: {_inflightCount}",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Владельцев: {_ownerCount}" +
                                       (_deadOwnerCount > 0
                                           ? $"   Уничтожено без Release: {_deadOwnerCount} (уйдут ближайшим sweep'ом)"
                                           : ""));

            if (_capacity > 0)
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(rect, (float)_survivorCount / _capacity,
                    $"Survivors {_survivorCount} / {_capacity}");
            }
            else
            {
                EditorGUILayout.HelpBox("SurvivorCapacity = 0: каждый Release сразу выгружает ассет.",
                    MessageType.None);
            }

            EditorGUILayout.Space(2f);
        }

        private void DrawTable()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Ассет", EditorStyles.miniBoldLabel);
                GUILayout.Label("Состояние", EditorStyles.miniBoldLabel, GUILayout.Width(StateWidth));
                GUILayout.Label("Ссылок", EditorStyles.miniBoldLabel, GUILayout.Width(RefsWidth));
                GUILayout.Label("Тип", EditorStyles.miniBoldLabel, GUILayout.Width(TypeWidth));
                GUILayout.Label("LRU", EditorStyles.miniBoldLabel, GUILayout.Width(LruWidth));
            }

            using var scope = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scope.scrollPosition;

            var shown = 0;
            foreach (var row in _rows)
            {
                if (!Matches(row)) continue;
                shown++;
                DrawRow(row);
            }

            if (shown == 0)
                EditorGUILayout.LabelField(_rows.Count == 0 ? "Индекс пуст." : "Ничего не найдено по фильтру.");
        }

        private void DrawRow(Row row)
        {
            var expanded = _expanded.Contains(row.Guid);

            using (new EditorGUILayout.HorizontalScope())
            {
                var toggled = EditorGUILayout.Foldout(expanded, row.Name, true);
                if (toggled != expanded)
                {
                    if (toggled) _expanded.Add(row.Guid);
                    else _expanded.Remove(row.Guid);
                }

                GUILayout.Label(StateLabel(row.State), GUILayout.Width(StateWidth));

                // Active без владельцев — аномалия: по контракту такой ref обязан лежать в survivors,
                // иначе он не попадёт под eviction и не выгрузится до Cleanup.
                var orphan = row.State == RefState.Active && row.Refs == 0;
                var color = GUI.color;
                if (orphan) GUI.color = Color.yellow;
                GUILayout.Label(row.Refs.ToString(), GUILayout.Width(RefsWidth));
                GUI.color = color;

                GUILayout.Label(row.TypeName, GUILayout.Width(TypeWidth));
                GUILayout.Label(row.LruPosition > 0 ? $"{row.LruPosition}/{_survivorCount}" : "—",
                    GUILayout.Width(LruWidth));
            }

            if (!expanded) return;

            EditorGUI.indentLevel += 2;
            EditorGUILayout.LabelField("GUID", row.Guid);

            if (GUILayout.Button("Показать в проекте", GUILayout.Width(160f)))
                PingAsset(row.Guid);

            if (row.Owners.Count == 0)
                EditorGUILayout.LabelField("Владельцы", "нет");
            else
                foreach (var owner in row.Owners)
                    EditorGUILayout.LabelField(" ", owner);

            EditorGUI.indentLevel -= 2;
        }

        private bool Matches(Row row) =>
            string.IsNullOrEmpty(_filter) ||
            row.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            row.Guid.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string StateLabel(RefState state) => state switch
        {
            RefState.Inflight => "LOAD",
            RefState.Active => "ACTIVE",
            _ => "SURVIVOR"
        };

        private static void PingAsset(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// Пересобирает снимок индекса. Считает ссылки одним проходом по <c>Locks</c>: словарь идёт
        /// owner→refs, а таблице нужен обратный срез ref→владельцы.
        /// </summary>
        private void Rebuild()
        {
            _rows.Clear();
            _loadedCount = _activeCount = _survivorCount = _inflightCount = 0;
            _ownerCount = _deadOwnerCount = 0;
            _capacity = AssetCache.Config?.SurvivorCapacity ?? 0;

            var model = AssetCache.Data;
            if (model == null) return;

            var owners = new Dictionary<AssetReference, List<string>>();
            foreach (var pair in model.Locks)
            {
                _ownerCount++;

                // Уничтоженный MonoBehaviour, не позвавший Release: его lock ещё держит ассеты, пока
                // очередной Release кого угодно не запустит sweep.
                var dead = pair.Key is Object uo && uo == null;
                if (dead) _deadOwnerCount++;

                var label = OwnerLabel(pair.Key, dead);
                foreach (var reference in pair.Value)
                {
                    if (!owners.TryGetValue(reference, out var list))
                    {
                        list = new List<string>();
                        owners[reference] = list;
                    }

                    list.Add(label);
                }
            }

            var lru = new Dictionary<AssetReference, int>();
            var position = 0;
            foreach (var reference in model.Survivors)
                lru[reference] = ++position;

            _survivorCount = model.Survivors.Count;
            _loadedCount = model.Handles.Count;
            _activeCount = _loadedCount - _survivorCount;
            _inflightCount = model.Inflight.Count;

            foreach (var pair in model.Inflight)
                _rows.Add(BuildRow(pair.Key, RefState.Inflight, pair.Value.Handle, 0, owners));

            foreach (var pair in model.Handles)
            {
                var survivor = lru.TryGetValue(pair.Key, out var slot);
                _rows.Add(BuildRow(pair.Key, survivor ? RefState.Survivor : RefState.Active,
                    pair.Value, survivor ? slot : 0, owners));
            }

            // Группируем по состоянию (порядок членов enum), внутри — по убыванию ссылок: сверху то,
            // что реально держат, снизу кандидаты на выгрузку.
            _rows.Sort((a, b) =>
            {
                var byState = a.State.CompareTo(b.State);
                if (byState != 0) return byState;
                if (a.State == RefState.Survivor) return a.LruPosition.CompareTo(b.LruPosition);
                var byRefs = b.Refs.CompareTo(a.Refs);
                return byRefs != 0 ? byRefs : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        private static Row BuildRow(AssetReference reference, RefState state, AsyncOperationHandle handle,
            int lruPosition, IReadOnlyDictionary<AssetReference, List<string>> owners)
        {
            var guid = reference?.AssetGUID ?? "";
            var row = new Row
            {
                Guid = guid,
                Name = ResolveName(guid, handle),
                TypeName = ResolveType(guid, handle),
                State = state,
                LruPosition = lruPosition
            };

            if (reference != null && owners.TryGetValue(reference, out var list))
            {
                row.Refs = list.Count;
                row.Owners.AddRange(list);
            }

            return row;
        }

        private static string ResolveName(string guid, AsyncOperationHandle handle)
        {
            if (handle.IsValid() && handle.IsDone && handle.Result is Object loaded && loaded != null)
                return loaded.name;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? guid : Path.GetFileNameWithoutExtension(path);
        }

        private static string ResolveType(string guid, AsyncOperationHandle handle)
        {
            if (handle.IsValid() && handle.IsDone && handle.Result is Object loaded && loaded != null)
                return loaded.GetType().Name;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return "—";

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null ? type.Name : "—";
        }

        private static string OwnerLabel(object owner, bool dead)
        {
            if (dead) return $"<уничтожен> ({owner.GetType().Name})";
            return owner is Object uo ? $"{uo.name} ({uo.GetType().Name})" : owner.GetType().Name;
        }
    }
}
#endif
