using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.UIComponents.Attributes;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.Components.Misc.LocalizationSystem
{
    /// <summary>
    /// Компонент для выставления фиксированного текста на UIComponent с возможностью локализации
    /// </summary>
    [RequireComponent(typeof(UIComponent)), ExecuteInEditMode]
    public class SetTextComponent : MonoBehaviour
    {
        [SerializeField, LocalizationKey] private string key;
        [SerializeField] private bool useLocalization = true;

        [SerializeField, AutoLink] private UIComponent uiComponent;

        [SerializeField, UIComponentLink(typeof(UIComponentText), "uiComponent"), TitleGroup("Link")]
        private int position = -1;

        private void OnEnable()
        {
            if (uiComponent == null)
            {
                Debug.LogError($"Target for SetText:{name} component is missing.");
                return;
            }

            Localization.OnLocalizationChanged += RefreshData;
            App.OnStart += RefreshData;
            Localization.OnInit += RefreshData;
            RefreshData();
        }

        private void OnDisable()
        {
            Localization.OnLocalizationChanged -= RefreshData;
            App.OnStart -= RefreshData;
            Localization.OnInit -= RefreshData;
        }

        [Button("Set Locale")]
        private void RefreshData()
        {
            var state = App.GetState();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                state = AppStates.Running;
                if (uiComponent == null)
                    return;
            }
#endif
            var text = "";
            if (state >= AppStates.Running || state == AppStates.Unfocused)
                text = useLocalization ? key.Translate() : key;

            if (position < 0)
                uiComponent.SetText(text);
            else
                uiComponent.SetText(text, position);
        }

#if UNITY_EDITOR
        [OnInspectorInit]
        private void RefreshOnInspectorInit() => RefreshData();
#endif
    }
}