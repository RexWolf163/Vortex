using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Базовое представление счётчика с текущим / минимальным / максимальным значениями.
    /// Умеет: рендер значений в <see cref="UIComponent"/> (по format-паттернам),
    /// заполнение массива <see cref="SliderView"/>, пульсация через <see cref="TweenerHub"/>
    /// на повышение/понижение, переключение <see cref="UIStateSwitcher"/> по бакетам
    /// заполнения (Empty / Less20 / Less50 / Less80 / Less100 / Fill).
    ///
    /// Подкласс отвечает за:
    /// <list type="bullet">
    ///   <item><see cref="GetValue"/> / <see cref="GetMinValue"/> / <see cref="GetMaxValue"/> —
    ///   извлечение чисел из модели <typeparamref name="T"/> (доступна через <see cref="Data"/>).</item>
    ///   <item><see cref="Init"/> — подписка на реактивные поля модели.</item>
    ///   <item><see cref="DeInit"/> — симметричная отписка.</item>
    ///   <item>Вызов <see cref="UpdateValue"/> / <see cref="UpdateMinValue"/> / <see cref="UpdateMaxValue"/>
    ///   из подписок при изменении соответствующих реактивных полей.</item>
    /// </list>
    ///
    /// Lifecycle: <see cref="OnEnable"/> подписывается на <see cref="IDataStorage.OnUpdateLink"/>
    /// и запускает <see cref="UpdateLink"/> для первичной инициализации. Любой ре-линк storage'а
    /// прогоняет тот же цикл: <see cref="DeInit"/> → перезабор <see cref="Data"/> → <see cref="Init"/> →
    /// refresh UI. <see cref="OnDisable"/> отвязывается.
    /// </summary>
    /// <typeparam name="T">Тип модели данных, из которой подкласс извлекает три числа.</typeparam>
    public abstract class CounterViewBase<T> : MonoBehaviour where T : class
    {
        /// <summary>
        /// Ссылка на класс-хранилище модели данных значения
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour sourceValue;

        private IDataStorage _storageValue;
        private IDataStorage StorageValue => _storageValue ??= sourceValue as IDataStorage;

        private T _data;

        /// <summary>
        /// Текущая модель, из которой подкласс тянет значения. Не хранить в подклассе —
        /// база сама переставляет ссылку при <see cref="UpdateLink"/>.
        /// </summary>
        protected T Data => _data ??= StorageValue.GetData<T>();

        [BoxGroup("Min Value UI")] [SerializeField]
        private UIComponent min;

        [BoxGroup("Min Value UI")] [SerializeField]
        private string patternMin = "{0}";

        [BoxGroup("Max Value UI")] [SerializeField]
        private UIComponent max;

        [BoxGroup("Max Value UI")] [SerializeField]
        private string patternMax = "{0}";

        [BoxGroup("Current Value UI")] [SerializeField]
        private UIComponent value;

        [BoxGroup("Current Value UI")] [SerializeField]
        private string patternValue = "{2} < {0} < {1}";


        [SerializeField] private SliderView[] sliders = new SliderView[0];

        [SerializeField] private TweenerHub tweenPulsation;

        [SerializeField, StateSwitcher(typeof(CounterStates))]
        private UIStateSwitcher switcher;

        /// <summary>
        /// Анимировать при повышении
        /// </summary>
        [SerializeField] private bool onUp = true;

        /// <summary>
        /// Анимировать при понижении
        /// </summary>
        [SerializeField] private bool onDown = false;

        /// <summary>
        /// Кеш текущего значения
        /// </summary>
        private int _cachedValue = Int32.MinValue;

        private void OnEnable()
        {
            StorageValue.OnUpdateLink += UpdateLink;
            switcher?.Set(CounterStates.Empty);
            UpdateLink();
        }

        private void OnDisable()
        {
            StorageValue.OnUpdateLink -= UpdateLink;
            DeInit();
            _cachedValue = Int32.MinValue;
        }

        private void UpdateLink()
        {
            // Полный ре-подписочный цикл: без DeInit подкласс остаётся подписан на СТАРЫЙ _data,
            // и апдейты из НОВОГО не приходят. -= на пустом event — no-op в C#, безопасно
            // даже до первого Init.
            DeInit();
            _data = StorageValue.GetData<T>();
            if (_data == null)
                return;

            // Синхронизация кэша ДО OnValueUpdated: без этого разница между старым _cachedValue
            // и значением новой модели может ложно активировать pulse-tween.
            _cachedValue = GetValue();
            Init();

            OnMinUpdated();
            OnMaxUpdated();
            OnValueUpdated();
        }

        /// <summary>
        /// Подписаться на реактивные поля <see cref="Data"/>. В обработчиках дёргать
        /// <see cref="UpdateValue"/> / <see cref="UpdateMinValue"/> / <see cref="UpdateMaxValue"/>.
        /// Зовётся базой после того, как <see cref="Data"/> уже перезабрана из storage.
        /// </summary>
        protected abstract void Init();

        /// <summary>
        /// Пересчёт текущего значения: pulse-tween, clamp по min/max, refresh UI-компонента
        /// значения, обновление всех <see cref="SliderView"/>-слайдеров, переключение
        /// <see cref="UIStateSwitcher"/> по проценту заполнения.
        /// </summary>
        private void OnValueUpdated()
        {
            var newValue = GetValue();
            if (tweenPulsation != null
                && ((newValue > _cachedValue && onUp) || (newValue < _cachedValue && onDown)))
                tweenPulsation.Pulse();
            _cachedValue = newValue;
            var minValue = GetMinValue();
            var maxValue = GetMaxValue();
            if (GetMinValue() > _cachedValue)
                _cachedValue = minValue;
            if (maxValue < _cachedValue)
                _cachedValue = maxValue;
            value?.SetText(string.Format(patternValue, _cachedValue, maxValue, minValue));
            if (sliders != null)
                foreach (var slider in sliders)
                    slider.Set(_cachedValue, maxValue, minValue);

            if (switcher != null)
            {
                if (_cachedValue == minValue)
                {
                    switcher.Set(CounterStates.Empty);
                    return;
                }

                if (_cachedValue == maxValue)
                {
                    switcher.Set(CounterStates.Fill);
                    return;
                }

                var diapason = maxValue - minValue;
                if (diapason <= 0)
                    return;
                var percent = 100 * (_cachedValue - minValue) / diapason;
                if (percent < 20)
                {
                    switcher.Set(CounterStates.Less20);
                    return;
                }

                if (percent < 50)
                {
                    switcher.Set(CounterStates.Less50);
                    return;
                }

                if (percent < 80)
                {
                    switcher.Set(CounterStates.Less80);
                    return;
                }

                switcher.Set(CounterStates.Less100);
            }
        }

        /// <summary>
        /// Пересчёт минимума: refresh <see cref="UIComponent"/> min-лейбла и слайдеров
        /// (min-порог у SliderView задаёт нижнюю границу диапазона).
        /// </summary>
        private void OnMinUpdated()
        {
            var newValue = GetMinValue();
            var maxValue = GetMaxValue();

            min?.SetText(string.Format(patternMin, newValue));
            if (sliders != null)
                foreach (var slider in sliders)
                    slider.Set(_cachedValue, maxValue, newValue);
        }

        /// <summary>
        /// Пересчёт максимума: refresh <see cref="UIComponent"/> max-лейбла и слайдеров
        /// (max задаёт верхнюю границу диапазона).
        /// </summary>
        private void OnMaxUpdated()
        {
            var minValue = GetMinValue();
            var newValue = GetMaxValue();

            max?.SetText(string.Format(patternMax, newValue));
            if (sliders != null)
                foreach (var slider in sliders)
                    slider.Set(_cachedValue, newValue, minValue);
        }


        /// <summary>
        /// Симметрично <see cref="Init"/>: отписаться от реактивных полей модели.
        /// Зовётся базой при OnDisable и при каждой смене storage-link ПЕРЕД переустановкой
        /// <see cref="Data"/>. Реализация должна быть идемпотентной: <c>-= handler</c> на
        /// не-подписанном event — no-op, но обращения к <c>_data</c> должны быть safe
        /// (например, через null-guard) — на первом вызове модель ещё не установлена.
        /// </summary>
        protected abstract void DeInit();

        /// <summary>Текущее значение из <see cref="Data"/>.</summary>
        protected abstract int GetValue();

        /// <summary>Нижняя граница диапазона из <see cref="Data"/>.</summary>
        protected abstract int GetMinValue();

        /// <summary>Верхняя граница диапазона из <see cref="Data"/>.</summary>
        protected abstract int GetMaxValue();

        /// <summary>
        /// Триггер полной перерисовки текущего значения из подписки. Зовётся подклассом
        /// в обработчике изменения реактивного поля значения.
        /// </summary>
        protected void UpdateValue() => OnValueUpdated();

        /// <summary>
        /// Триггер перерисовки минимума из подписки. Зовётся подклассом
        /// в обработчике изменения реактивного поля min-порога.
        /// </summary>
        protected void UpdateMinValue() => OnMinUpdated();

        /// <summary>
        /// Триггер перерисовки максимума из подписки. Зовётся подклассом
        /// в обработчике изменения реактивного поля max-порога.
        /// </summary>
        protected void UpdateMaxValue() => OnMaxUpdated();
    }
}