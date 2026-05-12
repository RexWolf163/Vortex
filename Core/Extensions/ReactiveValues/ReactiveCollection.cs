using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Контейнер для коллекций данных с реактивностью.
    /// Может быть закрыт на владельца, запрещая изменение данных другими объектами
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReactiveCollection<T> : IReactiveData
    {
        public event Action<IReadOnlyList<T>> OnUpdate;
        public event Action OnUpdateData;

        /// <summary>
        /// Владелец объекта
        /// Только он может вносить изменения в данные, если назначен
        /// </summary>
        protected object Owner;

        protected void CallOnUpdate()
        {
            OnUpdate?.Invoke(GetList());
            OnUpdateData?.Invoke();
        }

        [IsPOCO] protected List<T> Value { get; set; }
        public IReadOnlyList<T> GetList() => Value.AsReadOnly();

        public void Set(List<T> value, object owner = null)
        {
            if (!CheckLock(owner))
                return;
            Value = value.ToList();
            CallOnUpdate();
        }

        private bool CheckLock(object owner)
        {
            if (Owner != null && !Owner.Equals(owner))
            {
                if (owner == null)
                    Log.Print(LogLevel.Error, "Trying to change value without owner key.", this);
                else
                    Log.Print(LogLevel.Error, "Trying to change value from outer Object.", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Установить владельца данных
        /// (только он сможет менять содержимое контейнера)
        /// </summary>
        /// <param name="owner"></param>
        public void SetOwner(object owner)
        {
            if (owner == null)
                return;
            if (Owner != null)
            {
                Log.Print(LogLevel.Error, "Trying to set owner for busy ReactiveCollection container.", this);
                return;
            }

            Owner = owner;
        }

        public T this[int index] => Value[index];

        /// <summary>
        /// Принудительный запуск события обновления
        /// Использовать аккуратно!
        /// </summary>
        public void ForceUpdate() => CallOnUpdate();

        public void Set(int index, T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value[index] = value;
            CallOnUpdate();
        }

        public void Add(T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Add(value);
            CallOnUpdate();
        }

        public void Remove(T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            if (Value.Remove(value))
                CallOnUpdate();
        }

        public void RemoveAt(int value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.RemoveAt(value);
            CallOnUpdate();
        }

        public void Clear(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Clear();
            CallOnUpdate();
        }

        public void Sort(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Sort();
            CallOnUpdate();
        }

        public void RemoveRange(int index, int count, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.RemoveRange(index, count);
            CallOnUpdate();
        }

        public void Insert(int index, T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Insert(index, value);
            CallOnUpdate();
        }

        public void Reverse(int index, int count, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Reverse(index, count);
            CallOnUpdate();
        }

        public void Reverse(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Reverse();
            CallOnUpdate();
        }


#if UNITY_EDITOR
        /// <summary>
        /// Установка значения из Editor-инструментов.
        /// Обходит проверку владельца и дедупликацию.
        /// Не использовать в runtime-логике!
        /// </summary>
        public void EditorSet(List<T> value)
        {
            Value = value;
            CallOnUpdate();
        }
#endif
    }
}