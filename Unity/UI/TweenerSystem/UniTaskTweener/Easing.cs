using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem.UniTaskTweener
{
    public enum EaseType
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InBack,
        OutBack,
        InOutBack,
        InElastic,
        OutElastic,
        InOutElastic,
        InBounce,
        OutBounce,
        InOutBounce
    }

    public static class Easing
    {
        public static float Evaluate(EaseType type, float t) => type switch
        {
            EaseType.Linear => t,
            EaseType.InQuad => t * t,
            EaseType.OutQuad => 1f - (1f - t) * (1f - t),
            EaseType.InOutQuad => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f,
            EaseType.InCubic => t * t * t,
            EaseType.OutCubic => 1f - Mathf.Pow(1f - t, 3f),
            EaseType.InOutCubic => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f,
            EaseType.InBack => 2.70158f * t * t * t - 1.70158f * t * t,
            EaseType.OutBack => 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f),
            EaseType.InOutBack => t < 0.5f
                ? Mathf.Pow(2f * t, 2f) * (3.5949095f * 2f * t - 2.5949095f) / 2f
                : (Mathf.Pow(2f * t - 2f, 2f) * (3.5949095f * (2f * t - 2f) + 2.5949095f) + 2f) / 2f,
            EaseType.InElastic => t == 0f ? 0f
                : t >= 1f ? 1f
                : -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * (2f * Mathf.PI / 3f)),
            EaseType.OutElastic => t == 0f ? 0f
                : t >= 1f ? 1f
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f,
            EaseType.InOutElastic => t == 0f
                ? 0f
                : t >= 1f
                    ? 1f
                    : t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f))) / 2f
                        : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f)) / 2f +
                          1f,
            EaseType.InBounce => 1f - EvaluateBounceOut(1f - t),
            EaseType.OutBounce => EvaluateBounceOut(t),
            EaseType.InOutBounce => t < 0.5f
                ? (1f - EvaluateBounceOut(1f - 2f * t)) / 2f
                : (1f + EvaluateBounceOut(2f * t - 1f)) / 2f,
            _ => t
        };

        private static float EvaluateBounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            return t switch
            {
                < 1f / d1 => n1 * t * t,
                < 2f / d1 => n1 * (t -= 1.5f / d1) * t + 0.75f,
                < 2.5f / d1 => n1 * (t -= 2.25f / d1) * t + 0.9375f,
                _ => n1 * (t -= 2.625f / d1) * t + 0.984375f
            };
        }
    }
}