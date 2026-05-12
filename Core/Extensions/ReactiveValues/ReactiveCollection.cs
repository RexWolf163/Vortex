using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Реактивный контейнер для коллекций данных. Аналог <see cref="ReactiveValue{T}"/>, но
    /// хранит <see cref="List{T}"/> и публикует событие на каждом мутирующем действии
    /// (Set/Add/Remove/Clear/Insert/Sort/Reverse и т. д.).
    ///
    /// Может быть закрыт на владельца через <see cref="SetOwner"/>. После этого
    /// модификации, вызываемые с другим (или без) <c>owner</c>, отклоняются с логированием
    /// ошибки и не меняют коллекцию.
    ///
    /// Внутреннее хранилище <see cref="Value"/> помечено <see cref="IsPOCOAttribute"/> —
    /// контейнер сериализуется как обычный список через <c>SerializeController</c>.
    ///
    /// База абстракцию инициализирующих конструкторов <b>не предоставляет</b>: используется
    /// готовый наследник <see cref="ListData{T}"/>, который заводит пустой / переданный список.
    /// </summary>
    /// <typeparam name="T">Тип элемента коллекции. Сериализуется по правилам SerializeController.</typeparam>
    public abstract class ReactiveCollection<T> : IReactiveData
    {
        /// <summary>
        /// Типизированное событие: вызывается после любого мутирующего действия с актуальным
        /// read-only-снапшотом коллекции.
        /// </summary>
        public event Action<IReadOnlyList<T>> OnUpdate;

        /// <summary>
        /// Нетипизированное событие <see cref="IReactiveData"/>. Используется потребителями,
        /// которым не нужны сами данные — только факт изменения (например, перерисовка списка
        /// или перепроверка условия).
        /// </summary>
        public event Action OnUpdateData;

        /// <summary>
        /// Владелец коллекции. После назначения через <see cref="SetOwner"/> только он может
        /// мутировать коллекцию. <c>null</c> — коллекция открытая (любой может менять).
        /// </summary>
        protected object Owner;

        /// <summary>Внутреннее хранилище. Доступ через protected setter — только из наследников.</summary>
        [IsPOCO]
        protected List<T> Value { get; set; }

        /// <summary>Снимает события <see cref="OnUpdate"/> и <see cref="OnUpdateData"/> разом.</summary>
        protected void CallOnUpdate()
        {
            OnUpdate?.Invoke(GetList());
            OnUpdateData?.Invoke();
        }

        /// <summary>
        /// Возвращает read-only представление коллекции. Изменять напрямую нельзя — только через
        /// API контейнера (Set / Add / Remove / ...).
        /// </summary>
        public IReadOnlyList<T> GetList() => Value.AsReadOnly();

        /// <summary>Доступ к элементу по индексу (только чтение).</summary>
        public T this[int index] => Value[index];

        /// <summary>
        /// Полная замена содержимого коллекции копией <paramref name="value"/>.
        /// При несовпадении <paramref name="owner"/> с назначенным владельцем — ошибка, замена не выполняется.
        /// </summary>
        public void Set(List<T> value, object owner = null)
        {
            if (!CheckLock(owner))
                return;
            Value = value.ToList();
            CallOnUpdate();
        }

        /// <summary>Замена элемента по индексу.</summary>
        public void Set(int index, T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value[index] = value;
            CallOnUpdate();
        }

        /// <summary>Добавление элемента в конец коллекции.</summary>
        public void Add(T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Add(value);
            CallOnUpdate();
        }

        /// <summary>
        /// Удаление первого вхождения <paramref name="value"/>. Если элемент не найден — событие
        /// <b>не</b> вызывается (изменения не было).
        /// </summary>
        public void Remove(T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            if (Value.Remove(value))
                CallOnUpdate();
        }

        /// <summary>Удаление элемента по индексу.</summary>
        public void RemoveAt(int value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.RemoveAt(value);
            CallOnUpdate();
        }

        /// <summary>Удаление диапазона элементов начиная с <paramref name="index"/>.</summary>
        public void RemoveRange(int index, int count, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.RemoveRange(index, count);
            CallOnUpdate();
        }

        /// <summary>Очистка коллекции.</summary>
        public void Clear(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Clear();
            CallOnUpdate();
        }

        /// <summary>Вставка <paramref name="value"/> по индексу <paramref name="index"/>.</summary>
        public void Insert(int index, T value, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Insert(index, value);
            CallOnUpdate();
        }

        /// <summary>Сортировка по умолчанию (через <see cref="Comparer{T}.Default"/>).</summary>
        public void Sort(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Sort();
            CallOnUpdate();
        }

        /// <summary>Реверс диапазона элементов.</summary>
        public void Reverse(int index, int count, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Reverse(index, count);
            CallOnUpdate();
        }

        /// <summary>Реверс всей коллекции.</summary>
        public void Reverse(object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.Reverse();
            CallOnUpdate();
        }

        /// <summary>
        /// Принудительный вызов <see cref="OnUpdate"/> и <see cref="OnUpdateData"/> без
        /// модификации данных. Использовать редко — обычно нужен после внешней правки
        /// элементов по индексу через рефлексию или при принудительной перерисовке.
        /// </summary>
        public void ForceUpdate() => CallOnUpdate();

        /// <summary>
        /// Назначить владельца контейнера. Только он сможет мутировать коллекцию через любой
        /// из мутирующих методов. Повторное назначение запрещено — логирует ошибку.
        /// Передача <c>null</c> игнорируется.
        /// </summary>
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

        public void ReleaseOwner(object owner)
        {
            if (!CheckLock(owner))
                return;

            Owner = null;
        }

        /// <summary>
        /// Проверка владения. Возвращает <c>true</c>, если владельца нет либо
        /// <paramref name="owner"/> совпадает с назначенным. В противном случае логирует ошибку
        /// и возвращает <c>false</c>.
        /// </summary>
        private bool CheckLock(object owner)
        {
            if (Owner != null && !Owner.Equals(owner))
            {
                if (owner == null)
                    Log.Print(LogLevel.Error, "Trying to change data without owner key.", this);
                else
                    Log.Print(LogLevel.Error, "Trying to change data from outer Object.", this);
                return false;
            }

            return true;
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