using System;
using UnityEngine;
using Vortex.Core.LocalizationSystem;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Attributes;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.Components.Misc.StateSwitcher
{
    /// <summary>
    /// Элемент состояния свитчера: выставляет фиксированный текст на <see cref="UIComponent"/>.
    /// Логика по образцу SetTextComponent — ключ локализации + флаг useLocalization, адресация
    /// как в нём: <see cref="position"/> &lt; 0 — во все текстовые поля, иначе — в одно по индексу.
    /// </summary>
    [Serializable]
    public class TextSwitch : StateItem
    {
        [SerializeField, LocalizationKey] private string key;

        [SerializeField] private bool useLocalization = true;

        [SerializeField] private UIComponent uiComponent;

        [SerializeField, UIComponentLink(typeof(UIComponentText), "uiComponent")]
        private int position = -1;

        public override void Set()
        {
            var text = useLocalization ? key.Translate() : key;
            Apply(text);
        }

        public override void DefaultState()
        {
            Apply(string.Empty);
        }

        private void Apply(string text)
        {
            if (uiComponent == null)
                return;

            if (position < 0)
                uiComponent.SetText(text);
            else
                uiComponent.SetText(text, position);
        }

#if UNITY_EDITOR
        public override StateItem Clone()
        {
            return new TextSwitch
            {
                key = key,
                useLocalization = useLocalization,
                uiComponent = uiComponent,
                position = position
            };
        }

        public override string DropDownItemName => "Switch Text";
        public override string DropDownGroupName => "Text";
#endif
    }
}