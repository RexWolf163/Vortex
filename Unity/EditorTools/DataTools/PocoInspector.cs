#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Unity.EditorTools.DataTools
{
    /// <summary>
    /// Отрисовка POCO-моделей Vortex в editor-окнах: список свойств по правилам сериализатора и поля для их
    /// значений. Пользуются окна данных (<c>Tools/Vortex/SaveData/Global Index</c>, <c>Tools/Vortex/SaveData/Game Index</c>).
    ///
    /// Показываются свойства, которые сохраняет сериализатор: с getter и setter, у которых публичный getter или
    /// стоит <c>[IsPOCO]</c>, и нет <c>[NotPOCO]</c>. Простые типы редактируются полями; коллекции и вложенные
    /// объекты — только чтение, строкой сериализатора.
    /// </summary>
    public static class PocoInspector
    {
        private const BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Dictionary<Type, PropertyInfo[]> PropertiesCache = new();

        /// <summary>Свойства, которые сохраняет сериализатор Vortex, — то, что реально лежит в хранилище.</summary>
        public static PropertyInfo[] Properties(Type type)
        {
            if (PropertiesCache.TryGetValue(type, out var properties))
                return properties;

            properties = type.GetProperties(PropertyFlags)
                .Where(p => p.CanRead
                            && p.SetMethod != null
                            && p.GetIndexParameters().Length == 0
                            && p.GetCustomAttribute<NotPOCOAttribute>() == null
                            && (p.GetMethod is { IsPublic: true } || p.GetCustomAttribute<IsPOCOAttribute>() != null))
                .ToArray();
            PropertiesCache[type] = properties;
            return properties;
        }

        /// <summary>
        /// Поле свойства. <c>true</c> — значение изменено, новое — в <paramref name="result"/>. Типы без поля
        /// показываются строкой сериализатора, только чтение.
        /// </summary>
        public static bool DrawValue(PropertyInfo property, object target, out object result)
        {
            var type = property.PropertyType;
            var label = ObjectNames.NicifyVariableName(property.Name);
            var value = property.GetValue(target);
            result = value;

            EditorGUI.BeginChangeCheck();
            if (type == typeof(bool))
                result = EditorGUILayout.Toggle(label, (bool)value);
            else if (type == typeof(int))
                result = EditorGUILayout.IntField(label, (int)value);
            else if (type == typeof(long))
                result = EditorGUILayout.LongField(label, (long)value);
            else if (type == typeof(float))
                result = EditorGUILayout.FloatField(label, (float)value);
            else if (type == typeof(double))
                result = EditorGUILayout.DoubleField(label, (double)value);
            else if (type == typeof(string))
                result = EditorGUILayout.TextField(label, (string)value);
            else if (type.IsEnum)
                result = EditorGUILayout.EnumPopup(label, (Enum)value);
            else
            {
                EditorGUI.EndChangeCheck();
                DrawReadOnly(label, value);
                return false;
            }

            return EditorGUI.EndChangeCheck();
        }

        /// <summary>Значение без поля ввода: строка сериализатора в отключённой области.</summary>
        public static void DrawReadOnly(string label, object value)
        {
            EditorGUILayout.LabelField(label, value == null ? "null" : "только чтение");
            if (value == null)
                return;

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextArea(value.SerializeProperties());
        }
    }
}
#endif
