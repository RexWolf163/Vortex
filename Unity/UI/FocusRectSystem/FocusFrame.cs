using UnityEngine;

namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Поза View относительно исходной (растяжение по родителю без смещений).
    /// Не зависит от <see cref="FitMethod"/>: метод решает только, куда ляжет коэффициент <see cref="K"/>
    /// </summary>
    internal readonly struct FocusFrame
    {
        /// <summary>
        /// Исходная поза: смещение 0, коэффициент 1, поворот 0
        /// </summary>
        public static readonly FocusFrame Identity = new(Vector2.zero, 1f, 0f);

        /// <summary>
        /// Смещение пивота View от его положения в исходной позе, в координатах родителя
        /// </summary>
        public readonly Vector2 Offset;

        /// <summary>
        /// Коэффициент растяжения View
        /// </summary>
        public readonly float K;

        /// <summary>
        /// Поворот View по Z, градусы
        /// </summary>
        public readonly float Angle;

        public FocusFrame(Vector2 offset, float k, float angle)
        {
            Offset = offset;
            K = k;
            Angle = angle;
        }

        /// <summary>
        /// Промежуточная поза перехода. Без ограничения t — кривая пресета может выходить за 0..1
        /// </summary>
        public static FocusFrame Lerp(FocusFrame from, FocusFrame to, float t) => new(
            Vector2.LerpUnclamped(from.Offset, to.Offset, t),
            Mathf.LerpUnclamped(from.K, to.K, t),
            from.Angle + Mathf.DeltaAngle(from.Angle, to.Angle) * t);
    }
}
