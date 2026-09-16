using System;
using System.Collections.Generic;
using UnityEditor;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIBuilder.Sections;

namespace Vortex.Unity.UI.UIBuilder.Modules
{
    /// <summary>Модуль кнопки: <c>Vortex Primitives/Create Button</c>, секции локали, иконки и действия.</summary>
    public sealed class ButtonModule : UIBuilderModule<ButtonModuleSettings>
    {
        private const string MenuPath = "GameObject/Vortex Primitives/Create Button";

        public override string Title => "Button";

        public override IEnumerable<UIBuilderSection> CreateSections()
        {
            yield return new LocaleSection();
            yield return new IconSection();
            yield return new ActionSection();
        }

        [MenuItem(MenuPath, true)]
        private static bool OpenValidate() => UIBuilderController.CanOpen();

        [MenuItem(MenuPath, false, 2)]
        private static void Open(MenuCommand command) => UIBuilderController.Open<ButtonModule>(command);
    }

    [Serializable]
    public sealed class ButtonModuleSettings : UIBuilderModuleSettings
    {
        public ButtonModuleSettings() : base("Button")
        {
        }
    }
}
