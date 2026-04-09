using System;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Контейнер для данных с реактивностью.
    /// Может быть закрыт на владельца, запрещая изменение данных другими объектами
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ReactiveValue<T> : IReactiveData
    {
        public event Action<T> OnUpdate;
        public event Action OnUpdateData;

        /// <summary>
        /// Владелец объекта
        /// Только он может вносить изменения в данные, если назначен
        /// </summary>
        protected Object _owner;

        protected void CallOnUpdate()
        {
            OnUpdate?.Invoke(Value);
            OnUpdateData?.Invoke();
        }

        public T Value { get; protected set; }

        public void Set(T value, Object owner = null)
        {
            if (_owner != null && !_owner.Equals(owner))
            {
                Log.Print(LogLevel.Error, "Trying to change value from outer Object.", this);
                return;
            }

            Value = value;
            OnUpdate?.Invoke(Value);
            OnUpdateData?.Invoke();
        }

        /// <summary>
        /// Установить владельца данных
        /// (только он сможет менять содержимое контейнера)
        /// </summary>
        /// <param name="owner"></param>
        public void SetOwner(Object owner)
        {
            if (owner == null)
                return;
            if (_owner != null)
            {
                Log.Print(LogLevel.Error, "Trying to set owner for busy ReactiveData container.", this);
                return;
            }

            _owner = owner;
        }

        public static implicit operator T(ReactiveValue<T> reactive) => reactive.Value;
    }
}