using System;
using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Core.StateGraphSystem.Validation;

namespace Vortex.Core.StateGraphSystem.Controllers
{
    /// <summary>
    /// Исполнитель автомата <see cref="StateGraph"/>. Единственный, кто меняет модель.
    ///
    /// Вход в узел: запускаются условия всех его переходов, публикуется <see cref="StateGraph.CurrentNode"/>,
    /// затем проверяются автопереходы. Сигнал любого условия перепроверяет автопереходы в порядке приоритета;
    /// переходы «по Next» проверяются только по <see cref="Next"/>. Вход в узел без переходов завершает автомат.
    ///
    /// Активность: неактивный экземпляр игнорирует сигналы. Если при выключении состояние сохраняется
    /// (<see cref="Deactivate"/> с retainState), условия продолжают работать, а <see cref="ForceNode"/>
    /// и <see cref="Stop"/> принимаются; накопленное применяется при <see cref="Activate"/>.
    /// Иначе условия останавливаются, и активация запускает автомат со стартового узла.
    ///
    /// Граф проверяется <see cref="StateGraphValidator"/> один раз на экземпляр; с ошибками — не запускается.
    /// </summary>
    public static class StateGraphController
    {
        /// <summary>Ключ владельца реактивных полей модели</summary>
        internal static readonly object OwnerKey = new();

        #region Сеттеры модели

        internal static void SetCurrentNode(this StateGraph graph, string nodeId) =>
            graph.CurrentNode.Set(nodeId ?? string.Empty, OwnerKey);

        internal static void SetActive(this StateGraph graph, bool isActive) => graph.IsActive = isActive;

        #endregion

        #region Чтение

        /// <summary>Узел по Id (null — нет такого)</summary>
        public static StateNode GetNode(this StateGraph graph, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || graph.Nodes == null)
                return null;
            foreach (var node in graph.Nodes)
                if (node != null && node.Id == nodeId)
                    return node;
            return null;
        }

        /// <summary>Узел, опубликованный в <see cref="StateGraph.CurrentNode"/> (null — не запущен / завершён)</summary>
        public static StateNode GetCurrentNode(this StateGraph graph) => graph.GetNode(graph.CurrentNode.Value);

        #endregion

        #region Активность

        /// <summary>
        /// Включить экземпляр. Впервые, после выключения без сохранения состояния или после завершения —
        /// запуск со стартового узла. Иначе: узел, назначенный <see cref="ForceNode"/> в выключенном состоянии,
        /// входит сейчас; текущий узел перепроверяет автопереходы (условия могли выполниться за время простоя)
        /// </summary>
        public static void Activate(this StateGraph graph)
        {
            if (graph.IsActive)
                return;

            graph.SetActive(true);

            if (!graph.RetainWhileInactive || graph.Node == null)
            {
                graph.Start();
                return;
            }

            if (!graph.IsEntered)
            {
                Enter(graph, graph.Node);
                return;
            }

            EvaluateAuto(graph);
        }

        /// <summary>
        /// Выключить экземпляр. retainState = true — условия продолжают работать, принимаются
        /// <see cref="ForceNode"/> и <see cref="Stop"/>; false — условия останавливаются и сбрасываются
        /// (текущий узел остаётся опубликованным до следующего запуска).
        /// Выключенный с сохранением экземпляр можно выключить повторно без сохранения — так потребитель
        /// гасит работающие условия при окончательном освобождении (например, при уничтожении View)
        /// </summary>
        public static void Deactivate(this StateGraph graph, bool retainState)
        {
            if (!graph.IsActive && !graph.RetainWhileInactive)
                return;

            graph.SetActive(false);
            graph.RetainWhileInactive = retainState;
            if (!retainState)
                Exit(graph);
        }

        #endregion

        #region Сигналы

        /// <summary>Запуск (или перезапуск) со стартового узла. Только для активного экземпляра</summary>
        public static void Start(this StateGraph graph)
        {
            if (!graph.IsActive || !EnsureValid(graph))
                return;

            Exit(graph);
            Enter(graph, graph.Index[graph.StartNode]);
        }

        /// <summary>
        /// Сигнал «дальше»: первый по порядку переход «по Next», у которого выполнены условия.
        /// Не нашлось — реакция по <see cref="StateNode.NextMiss"/>. Неактивный или не запущенный экземпляр
        /// сигнал игнорирует
        /// </summary>
        public static void Next(this StateGraph graph)
        {
            if (!graph.IsActive || graph.Node == null || !graph.IsEntered || graph.IsEntering)
                return;

            var node = graph.Node;
            foreach (var transition in node.Transitions)
            {
                if (!transition.WaitNext || !Passes(transition))
                    continue;
                Transit(graph, transition.Target);
                return;
            }

            ReportNextMiss(graph, node);
        }

        /// <summary>
        /// Перейти в узел, указанный по Id или имени, минуя условия. На завершённом экземпляре — запуск с этого узла.
        /// В выключенном состоянии с сохранением — узел становится текущим и входит при активации
        /// </summary>
        public static void ForceNode(this StateGraph graph, string idOrName)
        {
            if (!graph.IsActive && !graph.RetainWhileInactive)
                return;
            if (!EnsureValid(graph))
                return;

            var node = Find(graph, idOrName);
            if (node == null)
            {
                Log.Print(LogLevel.Error, $"[StateGraph] {graph.Name}: node «{idOrName}» not found.", graph);
                return;
            }

            Exit(graph);
            if (graph.IsActive)
            {
                Enter(graph, node);
                return;
            }

            graph.Node = node;
            graph.SetCurrentNode(node.Id);
        }

        /// <summary>
        /// Принудительное завершение: условия останавливаются, <see cref="StateGraph.CurrentNode"/> пустеет,
        /// поднимается <see cref="StateGraph.OnFinished"/>. Не запущенный экземпляр — без действия
        /// </summary>
        public static void Stop(this StateGraph graph)
        {
            if (!graph.IsActive && !graph.RetainWhileInactive)
                return;
            if (graph.Node == null)
                return;

            Finish(graph);
        }

        #endregion

        #region Исполнение

        private static void Enter(StateGraph graph, StateNode node)
        {
            var version = ++graph.EntryVersion;
            graph.Node = node;
            graph.IsEntered = true;

            // Сигналы условий, поданные синхронно из Init, не обрабатываются до запуска всех условий узла:
            // иначе переход случился бы посреди запуска, и оставшиеся условия подписались бы уже после выхода.
            graph.IsEntering = true;
            try
            {
                foreach (var transition in node.Transitions)
                foreach (var condition in transition.Conditions)
                    condition.Init(() => OnConditionSignal(graph, version), graph.InstanceId);
            }
            finally
            {
                graph.IsEntering = false;
            }

            graph.SetCurrentNode(node.Id);

            // Подписчик CurrentNode мог синхронно увести автомат (Next / ForceNode / Stop)
            if (version != graph.EntryVersion)
                return;

            if (node.Transitions.Length == 0)
            {
                Finish(graph);
                return;
            }

            EvaluateAuto(graph);
        }

        /// <summary>Остановить условия текущего узла. Узел остаётся указателем исполнения</summary>
        private static void Exit(StateGraph graph)
        {
            graph.EntryVersion++;
            if (!graph.IsEntered || graph.Node == null)
            {
                graph.IsEntered = false;
                return;
            }

            graph.IsEntered = false;
            foreach (var transition in graph.Node.Transitions)
            foreach (var condition in transition.Conditions)
                condition.DeInit();
        }

        private static void Finish(StateGraph graph)
        {
            Exit(graph);
            graph.Node = null;
            graph.SetCurrentNode(string.Empty);
            graph.CallOnFinished();
        }

        private static void Transit(StateGraph graph, string targetId)
        {
            Exit(graph);
            Enter(graph, graph.Index[targetId]);
        }

        private static void OnConditionSignal(StateGraph graph, int version)
        {
            if (version != graph.EntryVersion || graph.IsEntering || !graph.IsActive)
                return;

            try
            {
                EvaluateAuto(graph);
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, $"[StateGraph] {graph.Name}: {e.Message}", graph);
            }
        }

        /// <summary>Первый по порядку автопереход с выполненными условиями</summary>
        private static void EvaluateAuto(StateGraph graph)
        {
            if (!graph.IsActive || graph.Node == null || !graph.IsEntered || graph.IsEntering)
                return;

            foreach (var transition in graph.Node.Transitions)
            {
                if (transition.WaitNext || !Passes(transition))
                    continue;
                Transit(graph, transition.Target);
                return;
            }
        }

        private static bool Passes(StateTransition transition)
        {
            foreach (var condition in transition.Conditions)
                if (!condition.Check())
                    return false;
            return true;
        }

        private static StateNode Find(StateGraph graph, string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName))
                return null;
            if (graph.Index.TryGetValue(idOrName, out var byId))
                return byId;
            foreach (var node in graph.Index.Values)
                if (node.Name == idOrName)
                    return node;
            return null;
        }

        private static void ReportNextMiss(StateGraph graph, StateNode node)
        {
            switch (node.NextMiss)
            {
                case NextMissBehavior.Warning:
                    Log.Print(LogLevel.Warning, $"[StateGraph] {graph.Name}: Next missed in node «{node.Name}».",
                        graph);
                    break;
                case NextMissBehavior.Error:
                    Log.Print(LogLevel.Error, $"[StateGraph] {graph.Name}: Next missed in node «{node.Name}».",
                        graph);
                    break;
            }
        }

        /// <summary>
        /// Проверка графа — один раз на экземпляр (структура в рантайме неизменна).
        /// Ошибки уходят в лог, экземпляр помечается непригодным
        /// </summary>
        private static bool EnsureValid(StateGraph graph)
        {
            if (graph.Index != null)
                return true;
            if (graph.IsInvalid)
                return false;

            var issues = StateGraphValidator.Validate(graph.Nodes, graph.StartNode);
            if (StateGraphValidator.HasErrors(issues))
            {
                foreach (var issue in issues)
                    if (issue.Severity == IssueSeverity.Error)
                        Log.Print(LogLevel.Error, $"[StateGraph] {graph.Name}: {issue.Message}", graph);
                graph.IsInvalid = true;
                return false;
            }

            var index = new Dictionary<string, StateNode>();
            foreach (var node in graph.Nodes)
                if (node != null)
                    index[node.Id] = node;
            graph.Index = index;
            return true;
        }

        #endregion
    }
}
