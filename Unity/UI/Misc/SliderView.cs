using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Компонент отображающий заданное значение на слайдере
    ///
    /// Внимание!
    /// Данные берутся НЕ со слайдера
    /// </summary>
    public class SliderView : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        [SerializeField, Range(0f, 3f)] private float delay;
        [SerializeField, Range(0f, 1f)] private float duration;

        [SerializeField] private EaseType ease = EaseType.InQuad;

        /// <summary>
        /// При включении сразу показывать заданное значение: в кадре включения значение выставляется
        /// без задержки и твина (в т.ч. заданное до <see cref="OnEnable"/>)
        /// </summary>
        [SerializeField] private bool instantOnEnable;

        private float target;

        /// <summary>
        /// Кадр последнего включения в режиме <see cref="instantOnEnable"/>; -1 — не было
        /// </summary>
        private int _enableFrame = -1;

        private readonly AsyncTween _tween = new();

        public void Set(float value, float max, float min = 0f)
        {
            if (Mathf.Approximately(target, value) && Mathf.Approximately(slider.maxValue, max))
                return;
            slider.minValue = min;
            slider.maxValue = max;
            target = value;
            if (_enableFrame == Time.frameCount)
                SetInstant();
            else if (delay > 0)
                TimeController.Call(Refresh, delay, this);
            else
                Refresh();
        }

        /// <summary>
        /// Значение без задержки и твина: отложенное выставление и идущий твин отменяются
        /// </summary>
        private void SetInstant()
        {
            TimeController.RemoveCall(this);
            _tween.Kill();
            slider.value = target;
        }

        private void Refresh()
        {
            if (duration == 0)
                slider.value = target;
            else
                _tween.SetSlider(slider, target, duration).SetEase(ease).SetToken(destroyCancellationToken)
                    .Run();
        }

        private void Awake()
        {
            slider.minValue = 0;
            slider.value = 0;
        }

        private void OnEnable()
        {
            TimeController.RemoveCall(this);
            if (!instantOnEnable)
                return;

            _enableFrame = Time.frameCount;
            // Значение задано до включения (в этом же кадре или раньше) — показать его сразу
            if (!Mathf.Approximately(target, float.MinValue))
                SetInstant();
        }

        private void OnDisable()
        {
            TimeController.Accumulate(() =>
            {
                target = float.MinValue;
                slider.value = 0;
            }, this);
        }

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
            _tween.Kill();
        }
    }
}