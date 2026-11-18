using Vortex.Core.System.Abstractions.SystemControllers;

namespace Vortex.Core.StateGraphSystem.Model
{
    /// <summary>
    /// Узел автомата. Стабильный <see cref="Id"/> — для ссылок переходов; уникальное <see cref="Name"/> —
    /// ключ, по которому потребитель сопоставляет узлу своё состояние.
    /// Узел без переходов — конечный: вход в него завершает автомат.
    /// </summary>
    public class StateNode : SystemModel
    {
        /// <summary>Стабильный идентификатор узла (на него ссылаются переходы)</summary>
        public string Id { get; internal set; }

        /// <summary>Уникальное имя-ключ узла</summary>
        public string Name { get; internal set; }

        /// <summary>Реакция на <c>Next</c>, не нашедший подходящего перехода</summary>
        public NextMissBehavior NextMiss { get; internal set; }

        /// <summary>Переходы в порядке приоритета</summary>
        public StateTransition[] Transitions { get; internal set; } = new StateTransition[0];
    }
}
