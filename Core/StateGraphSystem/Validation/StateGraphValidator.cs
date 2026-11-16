using System.Collections.Generic;
using Vortex.Core.StateGraphSystem.Model;

namespace Vortex.Core.StateGraphSystem.Validation
{
    /// <summary>
    /// Проверка структуры графа. Общая для рантайма (экземпляр с ошибками не запускается)
    /// и редактора (сводка пресета, визуализатор).
    ///
    /// Ошибки: нет стартового узла, пустой/повторяющийся Id, пустое/повторяющееся имя, переход на несуществующий узел,
    /// цикл из автопереходов (только автопереходы: цикл, разорванный хотя бы одним переходом «по Next», допустим).
    /// Предупреждения: узел недостижим из стартового; переход после безусловного, который никогда не выбирается.
    /// </summary>
    public static class StateGraphValidator
    {
        public static List<StateGraphIssue> Validate(IReadOnlyList<StateNode> nodes, string startNode)
        {
            var issues = new List<StateGraphIssue>();
            nodes ??= new StateNode[0];

            var byId = CollectNodes(nodes, issues);

            if (string.IsNullOrEmpty(startNode) || !byId.ContainsKey(startNode))
                issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.NoStartNode, null, -1,
                    "Start node is not set or not found."));

            foreach (var node in nodes)
                if (node != null)
                    CheckTransitions(node, byId, issues);

            CheckAutoCycles(byId, issues);

            if (!string.IsNullOrEmpty(startNode) && byId.ContainsKey(startNode))
                CheckReachability(startNode, byId, issues);

            return issues;
        }

        /// <summary>Есть ли среди замечаний ошибки</summary>
        public static bool HasErrors(IReadOnlyList<StateGraphIssue> issues)
        {
            foreach (var issue in issues)
                if (issue.Severity == IssueSeverity.Error)
                    return true;
            return false;
        }

        /// <summary>Индекс узлов по Id + проверка Id и имён на пустоту/уникальность</summary>
        private static Dictionary<string, StateNode> CollectNodes(IReadOnlyList<StateNode> nodes,
            List<StateGraphIssue> issues)
        {
            var byId = new Dictionary<string, StateNode>();
            var names = new HashSet<string>();
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (string.IsNullOrEmpty(node.Id))
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.InvalidId, null, -1,
                        $"Node «{node.Name}» has an empty Id."));
                else if (!byId.TryAdd(node.Id, node))
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.InvalidId, node.Id, -1,
                        $"Node «{node.Name}» duplicates Id of node «{byId[node.Id].Name}»."));

                if (string.IsNullOrEmpty(node.Name))
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.EmptyName, node.Id, -1,
                        $"Node #{node.Id} has an empty name."));
                else if (!names.Add(node.Name))
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.DuplicateName, node.Id, -1,
                        $"Node name «{node.Name}» is not unique."));
            }

            return byId;
        }

        /// <summary>Битые ссылки переходов и переходы, затенённые безусловными</summary>
        private static void CheckTransitions(StateNode node, Dictionary<string, StateNode> byId,
            List<StateGraphIssue> issues)
        {
            var transitions = node.Transitions ?? new StateTransition[0];
            // Безусловный автопереход уводит из узла сразу — после него не выбирается ничего.
            // Безусловный переход «по Next» забирает каждый Next — после него не выбирается ни один «по Next».
            var autoTaken = false;
            var nextTaken = false;
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                if (transition == null)
                    continue;

                if (string.IsNullOrEmpty(transition.Target) || !byId.ContainsKey(transition.Target))
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.DanglingTarget, node.Id, i,
                        $"Transition #{i} of node «{node.Name}» leads to a missing node."));

                if (autoTaken || (transition.WaitNext && nextTaken))
                    issues.Add(new StateGraphIssue(IssueSeverity.Warning, IssueKind.ShadowedTransition, node.Id, i,
                        $"Transition #{i} of node «{node.Name}» follows an unconditional one and is never taken."));

                if (transition.Conditions == null || transition.Conditions.Length == 0)
                {
                    if (transition.WaitNext)
                        nextTaken = true;
                    else
                        autoTaken = true;
                }
            }
        }

        /// <summary>Циклы по автопереходам (поиск в глубину с «серыми» узлами на стеке)</summary>
        private static void CheckAutoCycles(Dictionary<string, StateNode> byId, List<StateGraphIssue> issues)
        {
            // 0 — не посещён, 1 — на стеке, 2 — обработан
            var marks = new Dictionary<string, int>();
            var stack = new List<string>();
            foreach (var id in byId.Keys)
                if (!marks.ContainsKey(id))
                    VisitAuto(id, byId, marks, stack, issues);
        }

        private static void VisitAuto(string id, Dictionary<string, StateNode> byId, Dictionary<string, int> marks,
            List<string> stack, List<StateGraphIssue> issues)
        {
            marks[id] = 1;
            stack.Add(id);

            var transitions = byId[id].Transitions ?? new StateTransition[0];
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                if (transition == null || transition.WaitNext || string.IsNullOrEmpty(transition.Target) ||
                    !byId.ContainsKey(transition.Target))
                    continue;

                marks.TryGetValue(transition.Target, out var mark);
                if (mark == 1)
                    issues.Add(new StateGraphIssue(IssueSeverity.Error, IssueKind.AutoCycle, id, i,
                        $"Auto transitions form a cycle: {DescribeCycle(stack, transition.Target, byId)}."));
                else if (mark == 0)
                    VisitAuto(transition.Target, byId, marks, stack, issues);
            }

            stack.RemoveAt(stack.Count - 1);
            marks[id] = 2;
        }

        private static string DescribeCycle(List<string> stack, string entry, Dictionary<string, StateNode> byId)
        {
            var names = new List<string>();
            for (var i = stack.IndexOf(entry); i < stack.Count; i++)
                names.Add($"«{byId[stack[i]].Name}»");
            names.Add($"«{byId[entry].Name}»");
            return string.Join(" → ", names);
        }

        /// <summary>Узлы, недостижимые из стартового по любым переходам</summary>
        private static void CheckReachability(string startNode, Dictionary<string, StateNode> byId,
            List<StateGraphIssue> issues)
        {
            var reached = new HashSet<string> { startNode };
            var queue = new Queue<string>();
            queue.Enqueue(startNode);
            while (queue.Count > 0)
            {
                var transitions = byId[queue.Dequeue()].Transitions ?? new StateTransition[0];
                foreach (var transition in transitions)
                {
                    if (transition == null || string.IsNullOrEmpty(transition.Target) ||
                        !byId.ContainsKey(transition.Target))
                        continue;
                    if (reached.Add(transition.Target))
                        queue.Enqueue(transition.Target);
                }
            }

            foreach (var node in byId.Values)
                if (!reached.Contains(node.Id))
                    issues.Add(new StateGraphIssue(IssueSeverity.Warning, IssueKind.UnreachableNode, node.Id, -1,
                        $"Node «{node.Name}» is unreachable from the start node."));
        }
    }
}
