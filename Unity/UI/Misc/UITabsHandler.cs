using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Windows.MiscComponents
{
    /// <summary>
    /// Хэндлер переключения вкладок UI
    /// </summary>
    [RequireComponent(typeof(UIStateSwitcher))]
    public class UITabsHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private UIStateSwitcher tabsSwitcher;

        [SerializeField] private bool resetOnEnable = true;

        private void OnEnable()
        {
            if (resetOnEnable)
                tabsSwitcher.Set(0);
        }

        private void OnDisable()
        {
            if (resetOnEnable)
                tabsSwitcher.Set(0);
        }

        public void SetTab(int number)
        {
            if (number < 0 || number >= tabsSwitcher.States.Length)
                number = 0;
            tabsSwitcher.Set(number);
        }
    }
}