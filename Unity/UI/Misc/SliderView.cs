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

        [SerializeField, Range(0f, 1f)] private float duration;

        [SerializeField] private EaseType ease = EaseType.InQuad;

        private float target;

        private readonly AsyncTween _tween = new();

        public void Set(float value, float max, float min = 0f)
        {
            if (Mathf.Approximately(target, value) && Mathf.Approximately(slider.maxValue, max))
                return;
            slider.minValue = min;
            slider.maxValue = max;
            target = value;
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