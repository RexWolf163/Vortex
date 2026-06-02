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


        [SerializeField] private SliderView slider;

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
            _cachedValue = GetValue();
            var minValue = GetMinValue();
            var maxValue = GetMaxValue();
            if (slider != null) slider.Set(_cachedValue, maxValue, minValue);
            value?.SetText(string.Format(patternValue, _cachedValue, maxValue, minValue));
            min?.SetText(string.Format(patternMin, minValue));
            max?.SetText(string.Format(patternMax, maxValue));
            OnValueUpdated();
            Init();
        }

        private void OnDisable()
        {
            StorageValue.OnUpdateLink -= UpdateLink;
            DeInit();
            _cachedValue = Int32.MinValue;
        }

        private void UpdateLink()
        {
            _data = StorageValue.GetData<T>();
            OnMinUpdated();
            OnMaxUpdated();
            OnValueUpdated();
        }

        protected abstract void Init();

        /// <summary>
        /// Модель данных изменилась
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
            value?.SetText(string.Format(patternValue, newValue, maxValue, minValue));
            if (slider != null) slider.Set(_cachedValue, maxValue, minValue);

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
        /// Модель данных изменилась
        /// </summary>
        private void OnMinUpdated()
        {
            var newValue = GetMinValue();
            var maxValue = GetMaxValue();

            min?.SetText(string.Format(patternMin, newValue));
            if (slider != null) slider.Set(_cachedValue, maxValue, newValue);
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnMaxUpdated()
        {
            var minValue = GetMinValue();
            var newValue = GetMaxValue();

            max?.SetText(string.Format(patternMax, newValue));
            if (slider != null) slider.Set(_cachedValue, newValue, minValue);
        }


        protected abstract void DeInit();

        protected abstract int GetValue();
        protected abstract int GetMinValue();
        protected abstract int GetMaxValue();

        protected void UpdateValue() => OnValueUpdated();
        protected void UpdateMinValue() => OnMinUpdated();
        protected void UpdateMaxValue() => OnMaxUpdated();
    }
}