using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Core.System.Abstractions.SystemControllers;

namespace Vortex.Core.StateGraphSystem.Model
{
    /// <summary>
    /// Переход из узла в узел. Цель задаётся только Id узла.
    /// Автопереход (<see cref="WaitNext"/> = false) срабатывает сам, как только выполнены все условия;
    /// переход «по Next» проверяется только по вызову <c>Next</c>.
    /// Условия — по И; пустой список = условие всегда выполнено.
    /// </summary>
    public class StateTransition : SystemModel
    {
        /// <summary>Id узла-цели</summary>
        public string Target { get; internal set; }

        /// <summary>Переход ждёт сигнала <c>Next</c> (иначе — автопереход)</summary>
        public bool WaitNext { get; internal set; }

        /// <summary>Условия перехода (И). Пусто — безусловный</summary>
        public Condition[] Conditions { get; internal set; } = new Condition[0];
    }
}
