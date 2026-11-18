#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Vortex.Core.StateGraphSystem.Validation;

namespace Vortex.Unity.StateGraphSystem.Presets
{
    /// <summary>
    /// Замечание по пресету графа для редактора: тяжесть, текст, узел и сырой номер перехода
    /// (номер в массиве пресета, с учётом пустых элементов; -1 — не про переход)
    /// </summary>
    internal readonly struct StateGraphPresetIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string Message;
        public readonly string NodeId;
        public readonly int TransitionIndex;

        public StateGraphPresetIssue(IssueSeverity severity, string message, string nodeId, int transitionIndex)
        {
            Severity = severity;
            Message = message;
            NodeId = nodeId;
            TransitionIndex = transitionIndex;
        }
    }

    /// <summary>
    /// Редакторский API пресета: правка структуры (для визуализатора) и сбор замечаний (для сводки и визуализатора).
    /// Undo записывает вызывающий
    /// </summary>
    public partial class StateGraphPreset
    {
        /// <summary>Открыть пресет в визуализаторе. Назначается Editor-сборкой пакета</summary>
        internal static Action<StateGraphPreset> EditorOpenVisualizer;

        [Button("Open in visualizer"), PropertyOrder(4)]
        private void OpenVisualizer() => EditorOpenVisualizer?.Invoke(this);

        internal int EditorIndexOf(string nodeId)
        {
            for (var i = 0; i < nodes.Length; i++)
                if (nodes[i] != null && nodes[i].Id == nodeId)
                    return i;
            return -1;
        }

        /// <summary>Добавить узел. Первый узел пустого графа становится стартовым</summary>
        internal StateNodePreset EditorAddNode(string nodeName)
        {
            var node = StateNodePreset.EditorCreate(nodeName);
            var list = new List<StateNodePreset>(nodes) { node };
            nodes = list.ToArray();
            if (FindNode(startNode) == null)
                startNode = node.Id;
            EditorRefresh();
            return node;
        }

        /// <summary>Удалить узел вместе со всеми переходами на него</summary>
        internal void EditorRemoveNode(string nodeId)
        {
            var list = new List<StateNodePreset>(nodes);
            list.RemoveAll(node => node != null && node.Id == nodeId);
            nodes = list.ToArray();
            foreach (var node in nodes)
                node?.EditorRemoveTransitionsTo(nodeId);
            if (startNode == nodeId)
                startNode = null;
            EditorRefresh();
        }

        internal void EditorSetStartNode(string nodeId)
        {
            startNode = nodeId;
            EditorRefresh();
        }

        /// <summary>Имя, не занятое другими узлами: baseName, baseName 2, baseName 3…</summary>
        internal string EditorUniqueName(string baseName)
        {
            var name = baseName;
            for (var i = 2; HasName(name); i++)
                name = $"{baseName} {i}";
            return name;
        }

        private bool HasName(string nodeName)
        {
            foreach (var node in nodes)
                if (node != null && node.Name == nodeName)
                    return true;
            return false;
        }

        /// <summary>
        /// Все замечания: пустые элементы данных (в модель не попадают) + проверка графа.
        /// Номера переходов приведены к сырым номерам пресета
        /// </summary>
        internal List<StateGraphPresetIssue> EditorCollectIssues()
        {
            var issues = new List<StateGraphPresetIssue>();
            for (var n = 0; n < nodes.Length; n++)
            {
                var node = nodes[n];
                if (node == null)
                {
                    issues.Add(new StateGraphPresetIssue(IssueSeverity.Error, $"Node slot #{n} is empty.", null, -1));
                    continue;
                }

                var transitions = node.RawTransitions;
                for (var t = 0; t < transitions.Length; t++)
                {
                    var transition = transitions[t];
                    if (transition == null)
                    {
                        issues.Add(new StateGraphPresetIssue(IssueSeverity.Error,
                            $"Transition slot #{t} of node «{node.Label}» is empty.", node.Id, -1));
                        continue;
                    }

                    var conditions = transition.RawConditions;
                    for (var c = 0; c < conditions.Length; c++)
                        if (conditions[c] == null)
                            issues.Add(new StateGraphPresetIssue(IssueSeverity.Error,
                                $"Condition #{c} of transition #{t} of node «{node.Label}» has no type.", node.Id, t));
                }
            }

            foreach (var issue in StateGraphValidator.Validate(Nodes, startNode))
            {
                var transition = -1;
                if (issue.TransitionIndex >= 0)
                    transition = FindNode(issue.NodeId)?.EditorRawTransitionIndex(issue.TransitionIndex) ?? -1;
                issues.Add(new StateGraphPresetIssue(issue.Severity, issue.Message, issue.NodeId, transition));
            }

            return issues;
        }
    }
}
#endif
