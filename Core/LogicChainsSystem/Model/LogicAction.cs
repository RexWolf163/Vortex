using System;

namespace Vortex.Core.LogicChainsSystem.Model
{
    /// <summary>
    /// Абстракция класса логики, выполняемой на входе в этап цепочки
    /// </summary>
    [Serializable]
    public abstract class LogicAction
    {
        public abstract void Invoke();
    }
}