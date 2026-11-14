using System;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.CutsceneSystem.Models
{
    /// <summary>
    /// Управляющие данные показа — то, что контент катсцены знает о его ходе. Передаются в подгруженный префаб
    /// через его <c>DataStorage</c>. Пишет только <c>CutsceneBus</c>
    /// </summary>
    public class CutsceneControl : IReactiveData
    {
        /// <summary>
        /// Номер текущей фазы. 0 — ни одна фаза ещё не показана (пустое исходное состояние контента)
        /// </summary>
        public IntData Phase { get; }

        public event Action OnUpdateData
        {
            add => Phase.OnUpdateData += value;
            remove => Phase.OnUpdateData -= value;
        }

        internal CutsceneControl(IntData phase) => Phase = phase;
    }
}
