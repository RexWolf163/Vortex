using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Представление счетчика целочисленных данных
    /// расположенных в IDataStorage контейнерах
    /// Работает с IntData()
    /// </summary>
    public class CounterViewAdvanced : MonoBehaviour
    {
        private enum CounterStates
        {
            Empty,
            Less20,
            Less50,
            Less80,
            Less100,
            Fill
        }

        /// <summary>
        /// Ссылка на класс-хранилище модели данных значения
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink] [BoxGroup("DI fields")]
        private MonoBehaviour sourceValue;

        private IDataStorage _storageValue;
        private IDataStorage StorageValue => _storageValue ??= sourceValue as IDataStorage;

        /// <summary>
        /// Ссылка на класс-хранилище модели данных максимального значения
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink] [BoxGroup("DI fields")]
        private MonoBehaviour sourceMax;

        private IDataStorage _storageMax;
        private IDataStorage StorageMax => _storageMax ??= sourceMax as IDataStorage;

        /// <summary>
        /// Ссылка на класс-хранилище модели данных максимального значения
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink] [BoxGroup("DI fields")]
        private MonoBehaviour sourceMin;

        private IDataStorage _storageMin;
        private IDataStorage StorageMin => _storageMin ??= sourceMin as IDataStorage;

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
        private string patternValue = "{0}";


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
        [SerializeField] private bool onDawn = false;

        /// <summary>
        /// Значение
        /// </summary>
        private IntData _value;

        /// <summary>
        /// Минимальный порог
        /// </summary>
        private IntData _min;

        /// <summary>
        /// Максимальный порог
        /// </summary>
        private IntData _max;

        /// <summary>
        /// Кеш текущего значения
        /// </summary>
        private int _cachedValue = Int32.MinValue;

        private void OnEnable()
        {
            if (StorageValue != null)
                StorageValue.OnUpdateLink += UpdateLink;
            if (StorageMin != null)
                StorageMin.OnUpdateLink += UpdateLink;
            if (StorageMax != null)
                StorageMax.OnUpdateLink += UpdateLink;
            Init();
        }

        private void OnDisable()
        {
            DeInit();
            if (StorageValue != null)
                StorageValue.OnUpdateLink -= UpdateLink;
            if (StorageMin != null)
                StorageMin.OnUpdateLink -= UpdateLink;
            if (StorageMax != null)
                StorageMax.OnUpdateLink -= UpdateLink;
        }

        private void Init()
        {
            switcher?.Set(CounterStates.Empty);
            _value = StorageValue?.GetData<IntData>();
            _cachedValue = _value ?? 0;
            _min = StorageMin?.GetData<IntData>();
            _max = StorageMax?.GetData<IntData>();
            if (_value != null) _value.OnUpdate += OnValueUpdated;
            if (_min != null) _min.OnUpdate += OnMinUpdated;
            if (_max != null) _max.OnUpdate += OnMaxUpdated;
            var minValue = _min?.Value ?? 0f;
            var maxValue = _max?.Value ?? 1f;
            if (slider != null) slider.Set(_cachedValue, maxValue, minValue);
            value?.SetText(string.Format(patternValue, _cachedValue));
            min?.SetText(string.Format(patternMin, minValue));
            max?.SetText(string.Format(patternMax, maxValue));
            OnValueUpdated(_cachedValue);
        }

        private void DeInit()
        {
            if (_value != null) _value.OnUpdate -= OnValueUpdated;
            if (_min != null) _min.OnUpdate -= OnMinUpdated;
            if (_max != null) _max.OnUpdate -= OnMaxUpdated;
            _value = null;
            _min = null;
            _max = null;
            _cachedValue = Int32.MinValue;
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnValueUpdated(int newValue)
        {
            if (tweenPulsation != null
                && ((newValue > _cachedValue && onUp) || (newValue < _cachedValue && onDawn)))
                tweenPulsation.Pulse();
            _cachedValue = newValue;
            if (_min != null && _min.Value > _cachedValue)
                _cachedValue = _min.Value;
            if (_max != null && _max.Value < _cachedValue)
                _cachedValue = _max.Value;
            value?.SetText(string.Format(patternValue, newValue));
            if (slider != null) slider.Set(_cachedValue, _max?.Value ?? 1f, _min?.Value ?? 0f);

            if (_max != null && switcher != null)
            {
                if (_cachedValue == (_min?.Value ?? 0))
                {
                    switcher.Set(CounterStates.Empty);
                    return;
                }

                if (_cachedValue == _max)
                {
                    switcher.Set(CounterStates.Fill);
                    return;
                }

                var diapason = _max.Value - _min?.Value ?? 0;
                if (diapason <= 0)
                    return;
                var percent = 100 * _cachedValue / diapason;
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
        private void OnMinUpdated(int newValue)
        {
            min?.SetText(string.Format(patternMin, newValue));
            if (slider != null) slider.Set(_cachedValue, _max?.Value ?? 1f, _min?.Value ?? 0f);
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnMaxUpdated(int newValue)
        {
            max?.SetText(string.Format(patternMax, newValue));
            if (slider != null) slider.Set(_cachedValue, _max?.Value ?? 1f, _min?.Value ?? 0f);
        }
    }
}