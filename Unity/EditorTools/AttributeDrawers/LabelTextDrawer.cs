#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(LabelTextAttribute))]
    public class LabelTextDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;
            var attr = (LabelTextAttribute)attribute;

            data.IsCustomLabel();

            (var customLabel, _) = ReflectionHelper.ResolveTextOrMethod(property, attr.TextOrMethod);
            if (customLabel == null)
                data.HideLabel();
        }

        public override void RenderLabel(PropertyData data, PropertyAttribute attribute)
        {
            var position = data.Position;
            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);
            var property = data.Property;
            var attr = (LabelTextAttribute)attribute;
            (var customLabel, _) = ReflectionHelper.ResolveTextOrMethod(property, attr.TextOrMethod);
            var label = customLabel == null ? GUIContent.none : new GUIContent(customLabel);

            EditorGUI.LabelField(labelRect, label);
            data.Position.x += EditorGUIUtility.labelWidth;
            data.Position.width -= EditorGUIUtility.labelWidth;
        }
    }
}
#endif