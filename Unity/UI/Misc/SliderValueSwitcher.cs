using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Переключает UIComponent в зависимости от процента заполнения связанного слайдера
    /// Выводит процент слайдера в UIComponent 
    /// </summary>
    public class SliderValueSwitcher : MonoBehaviour
    {
        [SerializeField, AutoLink] private Slider slider;
        [SerializeField] private UIComponent uiComponent;

        [SerializeField] private string pattern = "{0:F0}%";
        [SerializeField] private int[] levels = { 50 };

        private void OnEnable()
        {
            slider.onValueChanged.AddListener(Refresh);
            Refresh(slider.value);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(Refresh);
        }

        private void Refresh(float value)
        {
            uiComponent.SetText(string.Format(pattern, slider.value * 100));
            if (levels == null)
                return;
            var state = levels.Length;
            var val = slider.value * 100;
            for (var i = 0; i < levels.Length; i++)
                if (val < levels[i])
                {
                    state = i;
                    break;
                }

            uiComponent.SetSwitcher(state);
        }
    }
}