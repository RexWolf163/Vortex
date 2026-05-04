#if UNITY_EDITOR
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;
using Vortex.Unity.ExtensibleEnumSystem.Attributes;

namespace Vortex.Unity.ExtensibleEnumSystem.Editor
{
    /// <summary>
    /// Drawer для <see cref="ExtEnumKeyAttribute"/>. На string-поле рисует popup с ключами оси.
    /// Перед чтением списка ключей принудительно прогоняет static-инициализатор оси,
    /// чтобы в Editor-режиме (без Play) дропдаун работал сразу после открытия Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(ExtEnumKeyAttribute))]
    public class ExtensibleEnumKeyAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[StateKey] only on string fields");
                return;
            }

            var attr = (ExtEnumKeyAttribute)attribute;

            // Принудительная инициализация типа: static-поля наследников создают инстансы,
            // регистрируясь в реестре ExtensibleEnum.
            try
            {
                RuntimeHelpers.RunClassConstructor(attr.ExtEnumType.TypeHandle);
            }
            catch
            {
                /* type initializer issues — fallback ниже */
            }

            var values = ExtensibleEnum.GetAll(attr.ExtEnumType);

            if (values == null || values.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                var rect = new Rect(position.x, position.y + position.height, position.width,
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(position,
                    $"Нет значений для оси {attr.ExtEnumType.Name}. Сохраните пресет.",
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