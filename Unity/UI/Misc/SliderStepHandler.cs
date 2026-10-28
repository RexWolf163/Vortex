using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Пошаговое изменение связанного слайдера: <see cref="Up"/> / <see cref="Down"/> сдвигают значение на 10%
    /// диапазона. Значение выставляется через сам слайдер — границы, wholeNumbers и onValueChanged работают как
    /// при перетаскивании
    /// </summary>
    public class SliderStepHandler : MonoBehaviour
    {
        /// <summary>
        /// Шаг — доля диапазона слайдера
        /// </summary>
        private const float Step = 0.1f;

        [SerializeField, AutoLink] private Slider slider;

        /// <summary>
        /// Прибавить 10% диапазона
        /// </summary>
        public void Up() => slider.normalizedValue += Step;

        /// <summary>
        /// Убавить 10% диапазона
        /// </summary>
        public void Down() => slider.normalizedValue -= Step;
    }
}
