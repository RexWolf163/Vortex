using System;

namespace Vortex.Core.LogicChainsSystem.Model
{
    /// <summary>
    /// Абстракция класса условия
    /// Реализация через наследников типа монолит
    /// </summary>
    [Serializable]
    public abstract class Condition
    {
        private Action _callback;

        /// <summary>
        /// Инициализация процесса контроля условия
        /// При выполнении условия отработает callback
        /// </summary>
        /// <param name="callback">Метод запуска проверки прочих условий (если их более одного)</param>
        public void Init(Action callback)
        {
            _callback = callback;
            Start();
        }

        protected void RunCallback() => _callback?.Invoke();

        protected abstract void Start();

        public abstract void DeInit();

        public abstract bool Check();
    }
}