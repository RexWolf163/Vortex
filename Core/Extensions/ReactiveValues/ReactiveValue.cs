using System;

namespace Vortex.Core.Extensions.ReactiveValues
{
    public abstract class ReactiveValue<T> : IReactiveData
    {
        public event Action<T> OnUpdate;
        public event Action OnUpdateData;

        protected void CallOnUpdate()
        {
            OnUpdate?.Invoke(Value);
            OnUpdateData?.Invoke();
        }

        public T Value { get; protected set; }

        public void Set(T value)
        {
            Value = value;
            OnUpdate?.Invoke(Value);
            OnUpdateData?.Invoke();
        }

        public static implicit operator T(ReactiveValue<T> reactive) => reactive.Value;
    }
}