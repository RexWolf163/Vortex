#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(HideLabelAttribute))]
    public class HideLabelDrawer : MultiDrawer
    {
        private string _customLabel;

        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.HideLabel();
        }
    }
}
#endif