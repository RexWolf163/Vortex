#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.LocalizationSystem.Editor
{
    public class LocalizationKeyAttributeDrawer : OdinAttributeDrawer<LocalizationKeyAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect(false, 50);
            var list = Localization.GetLocalizationKeys();
            ValueEntry.SmartValue =
                OdinDropdownTool.DropdownSelector(label, ValueEntry.SmartValue, list);

            var message = ValueEntry.SmartValue;
            message = ValueEntry.SmartValue.IsNullOrWhitespace()
                ? "<b>Empty. Choose a key!</b>"
                : message.Translate();
            if (message.Length > 320)
                message = message.Substring(0, 320) + "...";

            RichTextHelpBox.Create(rect, message,
                ValueEntry.SmartValue.IsNullOrWhitespace()
                    ? MessageType.Error
                    : MessageType.Info);
        }
    }
}
#endif