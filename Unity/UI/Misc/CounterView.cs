using System;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Unity.UI.Misc
{
    public abstract class CounterView : MonoBehaviour
    {
        [BoxGroup("Counter")] [SerializeField] private Text[] texts;
        [BoxGroup("Counter")] [SerializeField] private TextMeshProUGUI[] textsUiTMP;
        [BoxGroup("Counter")] [SerializeField] private TextMeshPro[] textsTMP;
        [BoxGroup("Counter")] [SerializeField] private TweenerHub[] tweeners;

        [BoxGroup("Counter")] [SerializeField] private string pattern = "{0}";

        [BoxGroup("MaxValue")] [SerializeField]
        private Text[] textsMax;

        [BoxGroup("MaxValue")] [SerializeField]
        private TextMeshProUGUI[] textsUiTMPMax;

        [BoxGroup("MaxValue")] [SerializeField]
        private TextMeshPro[] textsTMPMax;

        [BoxGroup("MaxValue")] [SerializeField]
        private TweenerHub[] tweenersMax;

        [BoxGroup("MaxValue")] [SerializeField]
        private string patternMax = "{0}";

        [SerializeField] private SliderView slider;

        /// <summary>
        /// Анимировать при повышении
        /// </summary>
        [SerializeField] private bool onUp = true;

        /// <summary>
        /// Анимировать при понижении
        /// </summary>
        [SerializeField] private bool onDawn = false;

        /// <summary>
        /// Кеш максимального значения
        /// </summary>
        protected int maxValue;

        /// <summary>
        /// Кеш текущего значения
        /// </summary>
        protected int value = Int32.MinValue;

        /// <summary>
        /// Получение значения из источника
        /// </summary>
        /// <returns></returns>
        protected abstract int GetValue();

        /// <summary>
        /// Получить максимальное значение из источника
        /// </summary>
        /// <returns></returns>
        protected abstract int? GetMaxValue();

        protected virtual void Awake()
        {
            if (pattern.IsNullOrWhitespace())
                pattern = "{0}";
        }

        protected virtual void OnEnable()
        {
            TimeController.Accumulate(Refresh, this);
        }

        protected virtual void OnDisable()
        {
            TimeController.Accumulate(() => value = Int32.MinValue, this);
        }

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
        }

        /// <summary>
        /// Обновить значение счетчика
        /// </summary>
        public virtual void Refresh()
        {
            var newMaxValue = GetMaxValue();
            var newVal = GetValue();

            if (newVal != value)
            {
                //Counter
                if (value > newVal && onUp || value < newVal && onDawn)
                    foreach (var tw in tweeners)
                        tw.Pulse();

                if (texts is { Length: > 0 })
                    foreach (var text in texts)
                        text.text = string.Format(pattern, newVal);

                if (textsTMP is { Length: > 0 })
                    foreach (var text in textsTMP)
                        text.text = string.Format(pattern, newVal);

                if (textsUiTMP is { Length: > 0 })
                    foreach (var text in textsUiTMP)
                        text.text = string.Format(pattern, newVal);

                value = newVal;
            }

            if (newMaxValue != maxValue)
            {
                //MaxValue
                if (maxValue > newMaxValue && onUp || maxValue < newMaxValue && onDawn)
                    foreach (var tw in tweeners)
                        tw.Pulse();

                if (textsMax is { Length: > 0 })
                    foreach (var text in textsMax)
                        text.text = string.Format(patternMax, newVal);

                if (textsTMPMax is { Length: > 0 })
                    foreach (var text in textsTMPMax)
                        text.text = string.Format(patternMax, newVal);

                if (textsUiTMPMax is { Length: > 0 })
                    foreach (var text in textsUiTMPMax)
                        text.text = string.Format(patternMax, newVal);

                maxValue = newMaxValue ?? 0;
            }

            //Slider
            if (newMaxValue != null && slider != null)
                slider.Set(newVal, newMaxValue.Value);
        }
    }
}