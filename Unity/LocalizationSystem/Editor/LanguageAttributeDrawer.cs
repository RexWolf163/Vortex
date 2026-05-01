#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.LocalizationSystem.Editor
{
    /// <summary>
    /// Odin-drawer для <see cref="LanguageAttribute"/>.
    /// Заменяет string-поле на SearchablePopup со списком зарегистрированных языков.
    /// </summary>
    public sealed class LanguageAttributeDrawer : OdinAttributeDrawer<LanguageAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            if (label != null)
                rect = EditorGUI.PrefixLabel(rect, label);

            var unityProp = Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath);
            if (unityProp == null || unityProp.propertyType != SerializedPropertyType.String)
            {
                SirenixEditorGUI.ErrorMessageBox("Only string is supported for Language Attribute");
                return;
            }

            var list = Localization.GetLanguages();
            var val = unityProp.stringValue;
            var currentIndex = val.IsNullOrWhitespace() ? -1 : list.IndexOf(val);
            DrawingUtility.DrawSelector(rect, unityProp, list.ToArray(), currentIndex: currentIndex);
        }
    }
}
#endif
