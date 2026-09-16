using System;
using UnityEngine;
using Vortex.Unity.Components.Misc.LocalizationSystem;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.UI.UIBuilder.Sections
{
    /// <summary>Секция кнопки: галочка стоит — добавляется <see cref="SetActionComponent"/>.</summary>
    [Serializable]
    public sealed class ActionSection : UIBuilderSection
    {
        [SerializeField] private bool addAction = true;

        public override Type PartType => typeof(UIComponentButton);

        public override string Title => "Действие";

        public override void Apply(UIComponent component)
        {
            if (addAction)
                AddLinked<SetActionComponent>(component);
        }
    }
}
