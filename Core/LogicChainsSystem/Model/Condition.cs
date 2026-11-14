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
        /// Цепочка-владелец. При необходимости может использоваться наследниками для фильтра событий
        /// </summary>
        protected string ChainOwner;

        /// <summary>
        /// Инициализация процесса контроля условия
        /// При выполнении условия отработает callback
        /// </summary>
        /// <param name="callback">Метод запуска проверки прочих условий (если их более одного)</param>
        /// <param name="chainOwner">Вызывающая логическая цепочка</param>
        public void Init(Action callback, string chainOwner)
        {
            _callback = callback;
            ChainOwner = chainOwner;
            Start();
        }

        protected void RunCallback() => _callback?.Invoke();

        protected abstract void Start();

        public abstract void DeInit();

        public abstract bool Check();
    }
}