#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;
using Vortex.Unity.ExtensibleEnumSystem.Attributes;

namespace Vortex.Unity.ExtensibleEnumSystem.Editor
{
    /// <summary>
    /// Drawer для <see cref="ExtEnumKeyAttribute"/>. На string-поле рисует popup с ключами
    /// ExtensibleEnum-типа. Реестр уже наполнен eager-инициализацией ExtensibleEnum
    /// (<c>[InitializeOnLoadMethod]</c> в Editor + <c>[RuntimeInitializeOnLoadMethod]</c>
    /// в рантайме), поэтому здесь просто читаем <see cref="ExtensibleEnum.GetAll(Type)"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(ExtEnumKeyAttribute))]
    public class ExtensibleEnumKeyAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[ExtEnumKey] only on string fields");
                return;
            }

            var attr = (ExtEnumKeyAttribute)attribute;
            var values = ExtensibleEnum.GetAll(attr.ExtEnumType);

            if (values == null || values.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.HelpBox(position,
                    $"Тип {attr.ExtEnumType.Name} не имеет зарегистрированных значений.",
                    MessageType.Warning);
                return;
            }

            var keys = values.Select(v => v.Key).ToArray();
            var current = property.stringValue;
            var index = Array.IndexOf(keys, current);
            var hadValid = index >= 0;
            if (!hadValid) index = 0;

            var newIndex = EditorGUI.Popup(position, label.text, index, keys);
            if (newIndex != index || !hadValid)
                property.stringValue = keys[newIndex];
        }
    }
}
#endif
