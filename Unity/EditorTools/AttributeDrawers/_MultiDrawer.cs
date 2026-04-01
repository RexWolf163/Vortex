#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Bus;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    public abstract class MultiDrawer : PropertyDrawer, IMultiDrawerAttribute
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InspectorController.RenderMultiAttribute(position, property, label);
        }

        public virtual void PreRender(PropertyData data, PropertyAttribute attribute)
        {
        }

        public virtual void RenderLabel(PropertyData data, PropertyAttribute attribute)
        {
        }

        public virtual void RenderField(PropertyData data, PropertyAttribute attribute)
        {
        }

        public virtual float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            return 0;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return InspectorController.GetAttributeHeight(property, label);
        }
    }
}
#endif