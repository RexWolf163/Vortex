#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    /// <summary>
    /// Native PropertyDrawer + IMultiDrawerAttribute для [ToggleBox].
    /// Скрывает поле если control value != state.
    ///
    /// В нативном пути группировка/бордер обеспечивается VortexRenderer.
    /// Drawer нужен как fallback для PropertyField-вызовов вне VortexRenderer
    /// и для корректной работы с EditorToolsDrawersController.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToggleBoxAttribute))]
    public class ToggleBoxDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            var tb = (ToggleBoxAttribute)attribute;
            var controlValue = ResolveControlValue(data.Property, tb.Control);
            if (controlValue != tb.State)
            {
                data.HideField();
                data.HideLabel();
            }
        }

        private static int ResolveControlValue(SerializedProperty property, string controlName)
        {
            // Sibling serialized property
            var controlProp = property.serializedObject.FindProperty(controlName);
            if (controlProp != null)
                return GetSerializedValue(controlProp);

            // Reflection fallback (C# property / method) — кешировано
            var target = property.serializedObject.targetObject;
            if (target == null) return -1;

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var csProp = ReflectionCache.GetProperty(type, controlName, flags);
            if (csProp != null && csProp.CanRead)
                return ConvertToInt(csProp.GetValue(target));

            var method = ReflectionCache.GetMethodNoArgs(type, controlName, flags);
            if (method != null)
                return ConvertToInt(method.Invoke(target, null));

            return -1;
        }

        private static int GetSerializedValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue ? 1 : 0;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Enum: return property.enumValueIndex;
                default: return -1;
            }
        }

        private static int ConvertToInt(object value)
        {
            if (value == null) return -1;
            if (value is bool b) return b ? 1 : 0;
            if (value is int i) return i;
            if (value is byte bt) return bt;
            if (value is Enum e) return Convert.ToInt32(e);
            return -1;
        }
    }
}
#endif