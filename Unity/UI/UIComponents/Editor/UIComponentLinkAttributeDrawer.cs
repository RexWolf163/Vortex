#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.UI.UIComponents.Attributes;

namespace Vortex.Unity.UI.UIComponents.Editor
{
    public class UIComponentLinkAttributeDrawer : OdinAttributeDrawer<UIComponentLinkAttribute, int>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var getter = ValueResolver.Get<object>(Property, Attribute.Getter);
            var source = getter.GetValue() as UIComponent;
            if (source == null)
            {
                Debug.LogError("Need UIComponent as parameter");
                return;
            }


            EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight / 2);
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            var componentType = Attribute.ComponentType;
            var parts = source.GetLinks(componentType);

            var error = true;
            var maxValue = parts.Length == 0 ? 0 : parts.Length - 1;

            var newValue = (int)EditorGUILayout.Slider(label, ValueEntry.SmartValue, -1, maxValue);
            ValueEntry.SmartValue = newValue;

            if (newValue < parts.Length && newValue >= 0)
                error = false;

            if (error)
            {
                if (ValueEntry.SmartValue == -1)
                    RichTextHelpBox.Create(rect, "Настройка будет применена ко всем компонентам", MessageType.Warning);
                else
                    RichTextHelpBox.Create(rect, "<b>Нет компонента-цели для данных</b>", MessageType.Error);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                var link = EditorGUI.ObjectField(rect, "Target", parts[newValue], typeof(GameObject), true);
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}
#endif