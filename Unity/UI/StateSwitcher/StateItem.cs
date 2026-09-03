using System;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.StateSwitcher
{
    /// <summary>
    /// Класс элемента состояния.
    /// Можно при наследовании расширять функционал для добавления управляемых элементов
    /// </summary>
    [Serializable]
    [ClassLabel("$DropDownItemName")]
    public abstract class StateItem : IDisposable
    {
        public abstract void Set();

        public abstract void DefaultState();

        public virtual void Dispose()
        {
            TimeController.RemoveCall(this);
        }

#if UNITY_EDITOR
        public abstract string DropDownItemName { get; }
        public abstract string DropDownGroupName { get; }
        public abstract StateItem Clone();
        
        /// <summary>
        /// Проверка на ошибки настроек.
        /// Возвращает false если есть ошибки
        /// </summary>
        /// <returns></returns>
        public abstract bool IsValid();
#endif
    }
}