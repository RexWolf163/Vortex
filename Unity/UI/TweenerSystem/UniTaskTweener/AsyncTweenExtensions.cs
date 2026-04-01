using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.TweenerSystem.UniTaskTweener
{
    /// <summary>
    /// Расширения-шорткаты для Set.
    /// Возвращают AsyncTween для продолжения цепочки перед Run().
    /// </summary>
    public static class AsyncTweenExtensions
    {
        /// <summary>
        /// Настроить анимацию localPosition
        /// </summary>
        public static AsyncTween SetLocalMove(this AsyncTween tween, Transform target, Vector3 to, float duration)
            => tween.Set(() => target.localPosition, v => target.localPosition = v, to, duration);

        /// <summary>
        /// Настроить анимацию position
        /// </summary>
        public static AsyncTween SetMove(this AsyncTween tween, Transform target, Vector3 to, float duration)
            => tween.Set(() => target.position, v => target.position = v, to, duration);

        /// <summary>
        /// Настроить анимацию localScale
        /// </summary>
        public static AsyncTween SetScale(this AsyncTween tween, Transform target, Vector3 to, float duration)
            => tween.Set(() => target.localScale, v => target.localScale = v, to, duration);

        /// <summary>
        /// Настроить анимацию значения Slider
        /// </summary>
        public static AsyncTween SetSlider(this AsyncTween tween, Slider slider, float to, float duration)
            => tween.Set(() => slider.value, v => slider.value = v, to, duration);

        /// <summary>
        /// Настроить анимацию alpha у CanvasGroup
        /// </summary>
        public static AsyncTween SetFade(this AsyncTween tween, CanvasGroup group, float to, float duration)
            => tween.Set(() => group.alpha, v => group.alpha = v, to, duration);

        /// <summary>
        /// Настроить анимацию цвета у Graphic (Image, Text, etc.)
        /// </summary>
        public static AsyncTween SetColor(this AsyncTween tween, Graphic graphic, Color to, float duration)
            => tween.Set(() => graphic.color, v => graphic.color = v, to, duration);

        /// <summary>
        /// Настроить анимацию sizeDelta у RectTransform
        /// </summary>
        public static AsyncTween SetSize(this AsyncTween tween, RectTransform rect, Vector2 to, float duration)
            => tween.Set(() => rect.sizeDelta, v => rect.sizeDelta = v, to, duration);

        /// <summary>
        /// Настроить анимацию anchoredPosition у RectTransform
        /// </summary>
        public static AsyncTween SetAnchoredMove(this AsyncTween tween, RectTransform rect, Vector2 to, float duration)
            => tween.Set(() => rect.anchoredPosition, v => rect.anchoredPosition = v, to, duration);

        /// <summary>
        /// Настроить анимацию pivot у RectTransform
        /// </summary>
        public static AsyncTween SetPivot(this AsyncTween tween, RectTransform rect, Vector2 to, float duration)
            => tween.Set(() => rect.pivot, v => rect.pivot = v, to, duration);
    }
}