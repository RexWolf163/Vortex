#if UNITY_EDITOR
using UnityEditor;

namespace Vortex.Unity.EditorTools
{
    internal static class InspectorHandler
    {
        public static bool IsPropertyNullable(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                case SerializedPropertyType.ObjectReference:
                    return true;
                default:
                    return false;
            }
        }

        internal static object GetPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.String: return property.stringValue;
                default: return null;
            }
        }
    }
}
#endif
