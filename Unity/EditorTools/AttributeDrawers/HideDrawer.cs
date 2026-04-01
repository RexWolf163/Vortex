#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(HideAttribute))]
    public class HideDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;
            var hideAttribute = (HideAttribute)attribute;
            if (string.IsNullOrEmpty(hideAttribute.Condition))
            {
                data.HideField();
                data.HideLabel();
                return;
            }

            var shouldHide = ReflectionHelper.InvokeBoolMethod(property, hideAttribute.Condition);
            if (shouldHide != true) return;

            data.HideField();
            data.HideLabel();
        }
    }
}
#endif