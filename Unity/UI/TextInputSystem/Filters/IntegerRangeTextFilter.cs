using System;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.TextInputSystem.Filters
{
    /// <summary>
    /// Целое число в диапазоне [min; max]: цифры и минус в начале (если min отрицательный).
    /// Во время набора отсекается только то, что вставкой символов уже не исправить: вставка цифр модуль не
    /// уменьшает, поэтому число за границей в сторону роста модуля — отказ. Число ближе к нулю, чем граница
    /// (набираю «5» при min = 10), допускается — дальше могут идти цифры. Положительное, кроме того, ещё может
    /// стать отрицательным минусом в начало. Итог — число в диапазоне
    /// </summary>
    [Serializable]
    public class IntegerRangeTextFilter : ITextInputFilter
    {
        [SerializeField, HorizontalGroup, LabelWidth(30)] private int min;

        [SerializeField, HorizontalGroup, LabelWidth(30)] private int max = 99;

        public bool IsValidPartial(string candidate)
        {
            if (candidate.Length == 0)
                return true;
            if (candidate == "-")
                return min < 0;
            if (!TryParse(candidate, out var value))
                return false;
            return value >= 0 ? value <= max || value <= -(long)min : value >= min;
        }

        public bool IsValidFinal(string text) => TryParse(text, out var value) && value >= min && value <= max;

        /// <summary>Только цифры с необязательным минусом в начале: без пробелов, «+» и разделителей</summary>
        private static bool TryParse(string text, out long value) =>
            long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) &&
            text[0] != '+';
    }
}
