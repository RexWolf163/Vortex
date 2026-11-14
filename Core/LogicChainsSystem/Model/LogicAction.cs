using System;

namespace Vortex.Core.LogicChainsSystem.Model
{
    /// <summary>
    /// Абстракция класса логики, выполняемой на входе в этап цепочки
    /// </summary>
    [Serializable]
    public abstract class LogicAction
    {
        protected string ChainOwner;

        public void Invoke(string guid)
        {
            ChainOwner = guid;
            Invoke();
        }

        public abstract void Invoke();
    }
}