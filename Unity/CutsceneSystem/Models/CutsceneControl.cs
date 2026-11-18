using System;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.CutsceneSystem.Models
{
    /// <summary>
    /// Управляющие данные показа — то, что внешний наблюдатель знает о его ходе. Пишет только
    /// <c>CutsceneBus</c> (по сообщению оркестратора)
    /// </summary>
    public class CutsceneControl : IReactiveData
    {
        /// <summary>
        /// Имя текущего узла графа катсцены. Пусто — показа нет
        /// </summary>
        public StringData Node { get; }

        public event Action OnUpdateData
        {
            add => Node.OnUpdateData += value;
            remove => Node.OnUpdateData -= value;
        }

        internal CutsceneControl(StringData node) => Node = node;
    }
}
