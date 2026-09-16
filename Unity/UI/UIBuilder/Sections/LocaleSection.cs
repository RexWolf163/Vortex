using System;
using UnityEngine;
using Vortex.Unity.Components.Misc.LocalizationSystem;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.UI.UIBuilder.Sections
{
    /// <summary>Секция текста: ключ задан — добавляется <see cref="SetTextComponent"/> с ключом.</summary>
    [Serializable]
    public sealed class LocaleSection : UIBuilderSection
    {
        [SerializeField, LocalizationKey] private string localeKey;

        [SerializeField] private bool useLocalization;

        public override Type PartType => typeof(UIComponentText);

        public override string Title => "Локаль";

        public override void Apply(UIComponent component)
        {
            if (string.IsNullOrEmpty(localeKey))
                return;

            AddLinked<SetTextComponent>(component, serialized =>
            {
                Find(serialized, "key").stringValue = localeKey;
                Find(serialized, "useLocalization").boolValue = useLocalization;
            });
        }
    }
}
