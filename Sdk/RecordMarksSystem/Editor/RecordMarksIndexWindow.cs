#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.RecordMarksSystem.Bus;

namespace Vortex.Sdk.RecordMarksSystem.Editor
{
    /// <summary>
    /// Read-only дамп текущего состояния <see cref="RecordMarksBus"/>:
    /// список всех известных меток (per-slot и per-account), количество помеченных GUID
    /// под каждой, разворачиваемый список GUID'ов. Данные существуют только в Play Mode:
    /// до Bootstrap'а bus'а модели не заполнены.
    ///
    /// Живёт под <c>#if UNITY_EDITOR</c> и через <see cref="EditorApplication.update"/>
    /// пересобирает снимок примерно 10 раз в секунду (аналог <c>AssetCacheIndexWindow</c>).
    /// </summary>
    public class RecordMarksIndexWindow : EditorWindow
    {
        [MenuItem("Tools/Vortex/Record Marks/Index")]
        public static void Open()
        {
            var w = GetWindow<RecordMarksIndexWindow>("Record Marks");
            w.minSize = new Vector2(400f, 300f);
        }

        private Vector2 _scroll;
        private string _filter = string.Empty;

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Ready: {RecordMarksBus.IsReady}",
                    RecordMarksBus.IsReady ? EditorStyles.boldLabel : EditorStyles.miniLabel,
                    GUILayout.Width(100f));
                _filter = EditorGUILayout.TextField("Filter", _filter);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Данные bus'а появляются только в Play Mode после Bootstrap.",
                    MessageType.Info);
                return;
            }

            if (!RecordMarksBus.IsReady)
            {
                EditorGUILayout.HelpBox(
                    "Bus ещё не готов: ждёт GlobalSaveController.OnInit и GameController.OnNewGame/OnLoadGame.",
                    MessageType.Warning);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                DrawSection("Slot marks", RecordMarksBus.EditorSlotCache);
                DrawSection("Global marks", RecordMarksBus.EditorGlobalCache);
            }
        }

        private void DrawSection(string title, IReadOnlyDictionary<string, HashSet<string>> cache)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (cache == null || cache.Count == 0)
            {
                EditorGUILayout.LabelField("  (empty)", EditorStyles.miniLabel);
                return;
            }

            foreach (var pair in cache)
            {
                if (!MatchesFilter(pair.Key)) continue;
                DrawMark(pair.Key, pair.Value);
            }
        }

        private bool MatchesFilter(string markName)
            => string.IsNullOrEmpty(_filter) ||
               markName.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static readonly HashSet<string> _expanded = new();

        private static void DrawMark(string markName, HashSet<string> guids)
        {
            using var _ = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                var expanded = _expanded.Contains(markName);
                var newExpanded = EditorGUILayout.Foldout(
                    expanded, $"{markName}   [{guids.Count}]", true);
                if (newExpanded && !expanded) _expanded.Add(markName);
                else if (!newExpanded && expanded) _expanded.Remove(markName);
            }
            if (!_expanded.Contains(markName)) return;

            EditorGUI.indentLevel++;
            foreach (var guid in guids)
                EditorGUILayout.SelectableLabel(guid,
                    EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUI.indentLevel--;
        }
    }
}
#endif
