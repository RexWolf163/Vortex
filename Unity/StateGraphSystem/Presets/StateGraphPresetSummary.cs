#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using Vortex.Core.StateGraphSystem.Validation;

namespace Vortex.Unity.StateGraphSystem.Presets
{
    /// <summary>
    /// Сводка графа в инспекторе пресета: состав и замечания проверки, чтобы ассет не был «чёрным ящиком».
    /// Пересчитывается после изменения пресета (OnValidate), а не на каждой перерисовке
    /// </summary>
    public partial class StateGraphPreset
    {
        private sealed class SummaryData
        {
            public int Nodes;
            public int EndNodes;
            public int AutoTransitions;
            public int NextTransitions;
            public int Conditions;
            public string StartName;
            public List<StateGraphPresetIssue> Issues;
        }

        [NonSerialized] private SummaryData _summary;

        private void ResetSummary() => _summary = null;

        [OnInspectorGUI, PropertyOrder(5)]
        private void DrawSummary()
        {
            _summary ??= BuildSummary();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Start", _summary.StartName ?? "—");
            EditorGUILayout.LabelField("Nodes", $"{_summary.Nodes} (end: {_summary.EndNodes})");
            EditorGUILayout.LabelField("Transitions",
                $"auto: {_summary.AutoTransitions}, Next: {_summary.NextTransitions}, conditions: {_summary.Conditions}");

            if (_summary.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Graph is valid.", MessageType.Info);
                return;
            }

            foreach (var issue in _summary.Issues)
                EditorGUILayout.HelpBox(issue.Message,
                    issue.Severity == IssueSeverity.Error ? MessageType.Error : MessageType.Warning);
        }

        private SummaryData BuildSummary()
        {
            var summary = new SummaryData { StartName = FindNode(startNode)?.Label };

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                summary.Nodes++;
                var transitions = node.RawTransitions;
                if (transitions.Length == 0)
                    summary.EndNodes++;

                foreach (var transition in transitions)
                {
                    if (transition == null)
                        continue;

                    if (transition.WaitNext)
                        summary.NextTransitions++;
                    else
                        summary.AutoTransitions++;
                    summary.Conditions += transition.RawConditions.Count(condition => condition != null);
                }
            }

            // Ошибки — первыми; порядок внутри тяжести сохраняется (сортировка устойчивая)
            summary.Issues = EditorCollectIssues().OrderByDescending(issue => issue.Severity).ToList();
            return summary;
        }
    }
}
#endif
