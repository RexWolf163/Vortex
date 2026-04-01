using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.Components.Misc.LocalizationSystem
{
    public class SetLocaleHandler : MonoBehaviour
    {
        [SerializeField] private UIComponent uiComponent;

        [SerializeField, Language] private string language;

        [SerializeField] private bool useSwitch;

        private void Awake()
        {
            uiComponent.SetAction(Run);
            Localization.OnInit += Refresh;
            Localization.OnLocalizationChanged += Refresh;
        }

        private void OnDestroy()
        {
            Localization.OnInit -= Refresh;
            Localization.OnLocalizationChanged -= Refresh;
        }

        public void Run()
        {
            if (!isActiveAndEnabled)
                return;
            Localization.SetCurrentLanguage(language);
        }

        [Button("Set Locale")]
        private void Refresh()
        {
            uiComponent.SetText(language.ToUpper().Translate());
            if (!useSwitch)
                return;
            uiComponent.SetSwitcher(
                Localization.GetCurrentLanguage() == language ? SwitcherState.On : SwitcherState.Off);
        }

        private void OnEnable() => Localization.OnLocalizationChanged += Refresh;

        private void OnDisable() => Localization.OnLocalizationChanged -= Refresh;
    }
}