using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem
{
    /// <summary>
    /// Неgeneric-основа <see cref="StateView{TEnum}"/>: хабы по состояниям и их переключение. Нужна, чтобы один
    /// редакторный drawer обслуживал поле с любым enum.
    /// </summary>
    [Serializable]
    public abstract class StateViewBase
    {
        [SerializeField, Tooltip("Хаб на каждое состояние, по порядку значений enum. Хаб выбранного состояния — Forward, остальные — Back.")]
        protected TweenerHub[] hubs = new TweenerHub[0];

        internal TweenerHub[] Hubs => hubs;

        /// <summary>Число состояний enum.</summary>
        internal abstract int Count { get; }

        /// <summary>Порядковый номер текущего состояния среди значений enum.</summary>
        internal abstract int CurrentIndex { get; }

        /// <summary>Значение enum по порядковому номеру (упакованное).</summary>
        internal abstract object ValueAt(int index);

        /// <summary>Имя состояния по порядковому номеру.</summary>
        internal abstract string NameAt(int index);

        /// <summary>Выставить состояние по порядковому номеру.</summary>
        internal abstract void SetIndex(int index, bool skip);

        /// <summary>
        /// Привести хабы к текущему состоянию: остальные — Back, хаб текущего — Forward. Пустые элементы
        /// пропускаются. <paramref name="skip"/> — без анимации; вне Play Mode хабы переключаются мгновенно сами.
        /// </summary>
        public void Apply(bool skip = false)
        {
            var current = CurrentIndex;
            for (var i = 0; i < hubs.Length; i++)
                if (i != current && hubs[i] != null)
                    hubs[i].Back(skip);

            if (current >= 0 && current < hubs.Length && hubs[current] != null)
                hubs[current].Forward(skip);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Подогнать длину массива под число состояний и заполнить пустые элементы. Обход с конца: при создании
        /// слоёв «первым в иерархии» они выстраиваются в порядке enum. Возвращает число заполненных элементов.
        /// </summary>
        internal int FillEmpty(Func<int, TweenerHub> create)
        {
            if (hubs.Length != Count)
                Array.Resize(ref hubs, Count);

            var filled = 0;
            for (var i = Count - 1; i >= 0; i--)
            {
                if (hubs[i] != null)
                    continue;
                hubs[i] = create(i);
                filled++;
            }

            return filled;
        }
#endif
    }

    /// <summary>
    /// Переключатель состояний на твинерах: на каждое значение <typeparamref name="TEnum"/> — свой
    /// <see cref="TweenerHub"/>. <see cref="Set"/> выставляет состояние: хаб выбранного — Forward, остальные — Back.
    ///
    /// Поле-класс для любого MonoBehaviour: <c>[SerializeField] private StateView&lt;MyState&gt; view;</c>.
    /// В инспекторе над полем — таблица состояний (клик по строке переключает) и кнопка Sync: пустым состояниям
    /// создаются дочерние слои с хабом, как по Ctrl+Alt+T.
    ///
    /// Жизненного цикла у поля нет: при включении хабы сами возвращаются в своё последнее положение (изначально
    /// Back). Чтобы привести их к сохранённому состоянию, владелец вызывает <see cref="StateViewBase.Apply"/>.
    /// </summary>
    [Serializable]
    public class StateView<TEnum> : StateViewBase where TEnum : struct, Enum
    {
        private static readonly TEnum[] Values = (TEnum[])Enum.GetValues(typeof(TEnum));

        [SerializeField, PropertyOrder(-1), Tooltip("Текущее состояние.")]
        private TEnum state;

        /// <summary>Текущее состояние.</summary>
        public TEnum State => state;

        /// <summary>
        /// Выставить состояние: хаб выбранного — Forward, остальные — Back. <paramref name="skip"/> — без анимации.
        /// </summary>
        public void Set(TEnum value, bool skip = false)
        {
            state = value;
            Apply(skip);
        }

        internal override int Count => Values.Length;

        internal override int CurrentIndex => Array.IndexOf(Values, state);

        internal override object ValueAt(int index) => Values[index];

        internal override string NameAt(int index) => Values[index].ToString();

        internal override void SetIndex(int index, bool skip) => Set(Values[index], skip);
    }
}
