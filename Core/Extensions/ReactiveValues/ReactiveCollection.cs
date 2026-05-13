using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Реактивный контейнер для коллекции <see cref="List{T}"/>: каждое успешное мутирующее
    /// действие (Set/Add/Remove/Clear/Insert/Sort/Reverse) публикует <see cref="OnUpdate"/>
    /// и <see cref="OnUpdateData"/>.
    ///
    /// Класс <b>абстрактный</b> и не имеет конструкторов — инициализация внутреннего
    /// хранилища <see cref="Value"/> делегирована наследникам. Канонический наследник —
    /// <see cref="ListData{T}"/> с пустым / копирующим конструктором.
    ///
    /// Может быть закрыт на владельца через <see cref="SetOwner"/>: после этого мутации,
    /// вызванные с другим (или без) <c>owner</c>, отклоняются с логированием ошибки.
    /// Освобождение — <see cref="ReleaseOwner"/>.
    ///
    /// Сериализация: <see cref="Value"/> помечено <see cref="IsPOCOAttribute"/>, благодаря
    /// чему protected-свойство попадает в <c>SerializeController</c> при обходе типа.
    ///
    /// Identity внутреннего списка сохраняется на протяжении жизни контейнера — все методы
    /// (включая полную замену через <see cref="Set(List{T},object)"/>) мутируют один и тот же
    /// <see cref="List{T}"/>. Ранее выданные через <see cref="GetList"/> read-only-обёртки
    /// остаются актуальными.
    /// </summary>
    /// <typeparam name="T">Тип элемента коллекции. Сериализуется по правилам SerializeController.</typeparam>
    public abstract class ReactiveCollection<T> : IReactiveData
    {
        /// <summary>
        /// Типизированное событие: вызывается после <b>успешного</b> мутирующего действия
        /// с актуальной read-only-обёрткой коллекции. Не вызывается, если мутация была
        /// отклонена <see cref="CheckLock"/>, или если фактического изменения не произошло
        /// (например, <see cref="Remove"/> для отсутствующего элемента).
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

        /// <summary>
        /// Внутреннее хранилище. Protected getter/setter — видно только наследникам;
        /// наследник <see cref="ListData{T}"/> в конструкторе создаёт <c>new List&lt;T&gt;()</c>.
        /// Помечено <see cref="IsPOCOAttribute"/> — это сигнал <c>SerializeController</c>'у
        /// включить protected-свойство в сериализацию (обычно он берёт только public).
        /// </summary>
        [IsPOCO]
        protected List<T> Value { get; set; }

        /// <summary>
        /// Размер вложенного списка
        /// </summary>
        public int Count => Value.Count;

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
        /// Полная замена содержимого коллекции элементами из <paramref name="value"/>.
        /// Сохраняет identity внутреннего списка (Clear + AddRange) — ранее выданные через
        /// <see cref="GetList"/> read-only-обёртки продолжают отражать актуальное состояние.
        /// При несовпадении <paramref name="owner"/> с назначенным владельцем — ошибка, замена не выполняется.
        /// </summary>
        public void Set(List<T> value, object owner = null)
        {
            if (!CheckLock(owner))
                return;
            Value.Clear();
            Value.AddRange(value);
            CallOnUpdate();
        }

        /// <summary>
        /// Замена элемента по индексу. Дедупликации нет — событие вызывается даже если
        /// значение совпадает с предыдущим. Невалидный <paramref name="index"/> бросит
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
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

        /// <summary>
        /// Удаление элемента по <paramref name="index"/>. Невалидный индекс бросит
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public void RemoveAt(int index, object owner = null)
        {
            if (!CheckLock(owner))
                return;

            Value.RemoveAt(index);
            CallOnUpdate();
        }

        /// <summary>
        /// Удаление <paramref name="count"/> элементов начиная с <paramref name="index"/>.
        /// Выход за границы бросит <see cref="ArgumentException"/>/<see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
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

        /// <summary>
        /// Сортировка по умолчанию (через <see cref="Comparer{T}.Default"/>). Если <typeparamref name="T"/>
        /// не реализует <see cref="IComparable{T}"/> / <see cref="IComparable"/> — бросит
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
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

        /// <summary>
        /// Освободить владельца контейнера. Принимает текущего владельца как «ключ» —
        /// при несовпадении логирует ошибку и ничего не делает. После успешного вызова
        /// коллекция снова открытая, и любой может назначить нового владельца через
        /// <see cref="SetOwner"/>.
        /// </summary>
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
        /// Обходит проверку владельца. Сохраняет identity внутреннего списка
        /// (Clear + AddRange) — ранее выданные read-only-обёртки остаются актуальными.
        /// Допускает <c>null</c> — коллекция очищается.
        /// Не использовать в runtime-логике!
        /// </summary>
        public void EditorSet(List<T> value)
        {
            Value.Clear();
            if (value != null) Value.AddRange(value);
            CallOnUpdate();
        }
#endif
    }
}