#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(ClassFilterAttribute))]
    public class ClassFilterAttributeDrawer : MultiDrawer
    {
        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;
            var filterAttr = (ClassFilterAttribute)attribute;
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning(
                    $"ClassFilterAttribute может использоваться только с полями типа UnityEngine.Object. " +
                    $"Поле '{property.name}' имеет тип {property.propertyType}");
                return;
            }

            var types = filterAttr.RequiredTypes;
            if (property.boxedValue == null)
                return;

            foreach (var type in types)
            {
                try
                {
                    var isValid = IsValidObject(property.objectReferenceValue, type);
                    if (isValid)
                        continue;
                    if (property.objectReferenceValue != null)
                    {
                        Debug.LogWarning(
                            $"Объект '{property.objectReferenceValue.name}' не соответствует типу {type.Name}. Поле очищено.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                property.objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }
        }

        private bool IsValidObject(Object obj, Type requiredType)
        {
            if (obj == null) return true;
            return obj switch
            {
                MonoBehaviour mb => requiredType.IsAssignableFrom(mb.GetType()),
                ScriptableObject so => requiredType.IsAssignableFrom(so.GetType()),
                _ => false
            };
        }
    }
}
#endif