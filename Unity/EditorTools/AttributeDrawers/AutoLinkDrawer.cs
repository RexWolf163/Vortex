#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(AutoLinkAttribute))]
    public class AutoLinkDrawer : MultiDrawer
    {
        private bool _errorLogged;

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            var position = data.Position;
            var property = data.Property;
            var fieldInfo = data.FieldInfo;
            var label = data.Label;
            if (!IsObjectReferenceField(fieldInfo))
            {
                var errorMsg =
                    $"[AutoLink] Field '{property.name}' is not a UnityEngine.Object type. AutoLink only works with Component, GameObject, or other UnityEngine.Object fields.";

                if (!_errorLogged)
                {
                    Debug.LogError(errorMsg, property.serializedObject.targetObject);
                    _errorLogged = true;
                }

                var helpHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(errorMsg), position.width);
                if (!onlyCalculation)
                    EditorGUI.HelpBox(new Rect(position.x, position.y, position.width, helpHeight), errorMsg,
                        MessageType.Error);

                var fieldRect = new Rect(position.x, position.y + helpHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width, EditorGUI.GetPropertyHeight(property, label, true));
                if (!onlyCalculation)
                    EditorGUI.PropertyField(fieldRect, property, label, true);
                return helpHeight;
            }

            // Автозаполнение при null
            if (property.objectReferenceValue == null) TryAutoLink(property, fieldInfo);
            return 0;
        }

        private bool IsObjectReferenceField(FieldInfo fieldInfo)
        {
            return typeof(Object).IsAssignableFrom(fieldInfo.FieldType);
        }

        private void TryAutoLink(SerializedProperty property, FieldInfo fieldInfo)
        {
            var target = property.serializedObject.targetObject;
            GameObject gameObject = null;

            if (target is Component component)
                gameObject = component.gameObject;
            else if (target is GameObject go)
                gameObject = go;

            if (gameObject == null)
                return;

            var fieldType = fieldInfo.FieldType;
            var classFilter = fieldInfo.GetCustomAttribute<ClassFilterAttribute>();

            if (classFilter != null)
            {
                // ClassFilter задаёт более точный тип — ищем по нему
                foreach (var requiredType in classFilter.RequiredTypes)
                {
                    var candidates = gameObject.GetComponents(fieldType);
                    foreach (var candidate in candidates)
                    {
                        if (requiredType.IsInstanceOfType(candidate))
                        {
                            property.objectReferenceValue = candidate;
                            property.serializedObject.ApplyModifiedProperties();
                            return;
                        }
                    }
                }
            }
            else
            {
                var found = gameObject.GetComponent(fieldType);
                if (found != null)
                {
                    property.objectReferenceValue = found;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
}
#endif