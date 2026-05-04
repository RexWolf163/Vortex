#if UNITY_EDITOR
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;
using Vortex.Unity.StateAxisSystem.Attributes;

namespace Vortex.Unity.StateAxisSystem.Editor
{
    /// <summary>
    /// Drawer для <see cref="StateKeyAttribute"/>. На string-поле рисует popup с ключами оси.
    /// Перед чтением списка ключей принудительно прогоняет static-инициализатор оси,
    /// чтобы в Editor-режиме (без Play) дропдаун работал сразу после открытия Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(StateKeyAttribute))]
    public class StateKeyAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[StateKey] only on string fields");
                return;
            }

            var attr = (StateKeyAttribute)attribute;

            // Принудительная инициализация типа: static-поля наследников создают инстансы,
            // регистрируясь в реестре StateAxis.
            try
            {
                RuntimeHelpers.RunClassConstructor(attr.AxisType.TypeHandle);
            }
            catch
            {
                /* type initializer issues — fallback ниже */
            }

            var values = StateAxis.GetAll(attr.AxisType);

            if (values == null || values.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                var rect = new Rect(position.x, position.y + position.height, position.width,
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(position,
                    $"Нет значений для оси {attr.AxisType.Name}. Сохраните пресет.",
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