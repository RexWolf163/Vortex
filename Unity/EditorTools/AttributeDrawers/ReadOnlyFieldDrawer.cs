#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(ReadOnlyFieldAttribute))]
    public class ReadOnlyFieldDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var position = data.Position;
            var property = data.Property;

            // Используем DisabledScope для визуального отображения недоступности
            using (new EditorGUI.DisabledScope(true))
                DrawingUtility.DrawDefaultField(position, data, property);
        }
    }
}
#endif