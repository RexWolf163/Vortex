using System;
using System.Collections.Generic;
using UnityEditor;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIBuilder.Sections;

namespace Vortex.Unity.UI.UIBuilder.Modules
{
    /// <summary>Модуль текста: <c>Vortex Primitives/Create Text</c>, секция локали.</summary>
    public sealed class TextModule : UIBuilderModule<TextModuleSettings>
    {
        private const string MenuPath = "GameObject/Vortex Primitives/Create Text";

        public override string Title => "Text";

        public override IEnumerable<UIBuilderSection> CreateSections()
        {
            yield return new LocaleSection();
        }

        [MenuItem(MenuPath, true)]
        private static bool OpenValidate() => UIBuilderController.CanOpen();

        [MenuItem(MenuPath, false, 1)]
        private static void Open(MenuCommand command) => UIBuilderController.Open<TextModule>(command);
    }

    [Serializable]
    public sealed class TextModuleSettings : UIBuilderModuleSettings
    {
        public TextModuleSettings() : base("Text")
        {
        }
    }
}
