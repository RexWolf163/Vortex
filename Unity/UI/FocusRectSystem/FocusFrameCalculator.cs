using System;
using UnityEngine;

namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Расчёт позы View, при которой фрагмент, размеченный FocusRect в пространстве родителя View,
    /// стоит по центру родителя, вписан по правилу и не наклонён.
    /// Результат зависит только от родителя, FocusRect и правила — не от текущей позы View
    /// </summary>
    internal static class FocusFrameCalculator
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Расчёт кадра. Вырожденная геометрия — исключение
        /// </summary>
        /// <param name="view">управляемый RectTransform</param>
        /// <param name="focusRect">разметка фрагмента</param>
        /// <param name="rule">правило вписывания</param>
        public static FocusFrame Calculate(RectTransform view, RectTransform focusRect, FitRule rule)
        {
            var parent = GetParent(view);
            var parentRect = parent.rect;
            if (parentRect.width <= 0f || parentRect.height <= 0f)
                throw new InvalidOperationException(
                    $"[FocusRectView] Нулевой размер родителя {parent.name} ({parentRect.size}) у {view.name}");

            // Фрагмент в координатах родителя: углы GetWorldCorners — BL, TL, TR, BR
            focusRect.GetWorldCorners(Corners);
            Vector2 bottomLeft = parent.InverseTransformPoint(Corners[0]);
            Vector2 topLeft = parent.InverseTransformPoint(Corners[1]);
            Vector2 topRight = parent.InverseTransformPoint(Corners[2]);
            Vector2 bottomRight = parent.InverseTransformPoint(Corners[3]);

            var center = (bottomLeft + topRight) * 0.5f;
            var widthAxis = bottomRight - bottomLeft;
            var width = widthAxis.magnitude;
            var height = (topLeft - bottomLeft).magnitude;
            if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
                throw new InvalidOperationException(
                    $"[FocusRectView] Нулевой размер FocusRect {focusRect.name} ({width}x{height}) у {view.name}");

            var scaleX = parentRect.width / width;
            var scaleY = parentRect.height / height;
            var k = rule == FitRule.Envelope ? Mathf.Max(scaleX, scaleY) : Mathf.Min(scaleX, scaleY);

            // Компенсация наклона FocusRect: после поворота фрагмент стоит ровно
            var angle = -Mathf.Atan2(widthAxis.y, widthAxis.x) * Mathf.Rad2Deg;

            // Точка View, лежавшая в исходной позе в центре фрагмента, после растяжения и поворота
            // вокруг пивота должна прийти в центр родителя
            var pivotDefault = parentRect.min + Vector2.Scale(parentRect.size, view.pivot);
            Vector2 rotated = Quaternion.Euler(0f, 0f, angle) * ((center - pivotDefault) * k);
            var pivotTarget = parentRect.center - rotated;

            return new FocusFrame(pivotTarget - pivotDefault, k, angle);
        }

        /// <summary>
        /// Родитель View, в пространстве которого размечается фрагмент
        /// </summary>
        public static RectTransform GetParent(RectTransform view)
        {
            if (view.parent is RectTransform parent)
                return parent;
            throw new InvalidOperationException($"[FocusRectView] У {view.name} нет родителя-RectTransform");
        }
    }
}
