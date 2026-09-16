using System;
using UnityEngine;
using Vortex.Unity.Components.Misc.LocalizationSystem;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.UI.UIBuilder.Sections
{
    /// <summary>Секция графики: спрайт задан — добавляется <see cref="SetSpriteComponent"/> со спрайтом.</summary>
    [Serializable]
    public sealed class IconSection : UIBuilderSection
    {
        [SerializeField] private Sprite icon;

        public override Type PartType => typeof(UIComponentGraphic);

        public override string Title => "Иконка";

        public override void Apply(UIComponent component)
        {
            if (icon == null)
                return;

            AddLinked<SetSpriteComponent>(component,
                serialized => Find(serialized, "sprite").objectReferenceValue = icon);
        }
    }
}
