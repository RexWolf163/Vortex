#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.Elements
{
    internal static class ReflectionHelper
    {
        private const BindingFlags AllInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>
        /// Находит метод на объекте (public/private, instance/static). Кешировано.
        /// </summary>
        internal static MethodInfo FindMethod(object owner, string name) =>
            ReflectionCache.GetMethod(owner.GetType(), name, AllInstance);

        /// <summary>
        /// Возвращает владельца поля с fallback на targetObject.
        /// </summary>
        internal static object ResolveOwner(SerializedProperty property) =>
            InspectorHandler.GetFieldOwner(property) ?? property.serializedObject.targetObject;

        /// <summary>
        /// Вызывает метод на владельце поля, с fallback на targetObject.
        /// Возвращает null если метод не найден или выбросил исключение.
        /// </summary>
        internal static T InvokeMethod<T>(SerializedProperty property, string methodName) where T : class
        {
            var owner = ResolveOwner(property);

            var method = FindMethod(owner, methodName);
            if (method == null && owner != property.serializedObject.targetObject)
            {
                owner = property.serializedObject.targetObject;
                method = FindMethod(owner, methodName);
            }

            if (method == null) return null;

            try
            {
                return method.Invoke(method.IsStatic ? null : owner, null) as T;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Обрабатывает строку с $-префиксом: если начинается с $ — вызывает метод,
        /// иначе возвращает как есть.
        /// Возвращает (text, isError). При ошибке text = сообщение об ошибке.
        /// </summary>
        internal static (string text, bool isError) ResolveTextOrMethod(SerializedProperty property,
            string textOrMethod)
        {
            if (!textOrMethod.StartsWith("$"))
                return (textOrMethod, false);

            var methodName = textOrMethod.Substring(1);
            var owner = ResolveOwner(property);

            var method = FindMethod(owner, methodName);
            if (method == null && owner != property.serializedObject.targetObject)
            {
                owner = property.serializedObject.targetObject;
                method = FindMethod(owner, methodName);
            }

            if (method == null || method.ReturnType != typeof(string))
                return (null, true);

            try
            {
                var result = (string)method.Invoke(method.IsStatic ? null : owner, null);
                return (result, false);
            }
            catch
            {
                return (null, true);
            }
        }

        /// <summary>
        /// Вызывает bool-метод на targetObject (для hideIf и аналогичных).
        /// Возвращает null если метод не найден. Кешировано.
        /// </summary>
        internal static bool? InvokeBoolMethod(SerializedProperty property, string methodName)
        {
            var owner = ResolveOwner(property);
            var method = ReflectionCache.GetMethod(owner.GetType(), methodName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            if (method != null && method.ReturnType == typeof(bool))
                return (bool)method.Invoke(method.IsStatic ? null : owner, null);

            return null;
        }

        /// <summary>
        /// Вызывает OnValueChanged-метод если на поле есть атрибут [OnValueChanged].
        /// Поддерживает сигнатуры: () и (T value).
        /// </summary>
        internal static void InvokeOnValueChanged(SerializedProperty property)
        {
            var fieldInfo = InspectorHandler.GetFieldInfo(property);
            if (fieldInfo == null) return;

            var attr = fieldInfo.GetCustomAttribute<OnChangedAttribute>();
            if (attr == null) return;

            var owner = InspectorHandler.GetFieldOwner(property);
            if (owner == null) return;

            var method = ReflectionCache.GetMethod(owner.GetType(), attr.MethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null) return;

            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 0)
                {
                    method.Invoke(owner, null);
                }
                else if (parameters.Length == 1)
                {
                    var value = InspectorHandler.GetPropertyValue(property);
                    if (value != null && parameters[0].ParameterType.IsInstanceOfType(value))
                        method.Invoke(owner, new[] { value });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OnValueChanged] Error invoking '{attr.MethodName}': {e.Message}");
            }
        }

        /// <summary>
        /// Резолвит $MethodName на объекте-владельце (не SerializedProperty).
        /// Поддерживает опциональный int-параметр (index).
        /// Используется CollectionRenderer для ClassLabel.
        /// </summary>
        internal static string ResolveTextOrMethodOnOwner(object owner, string textOrMethod, int index = -1)
        {
            if (!textOrMethod.StartsWith("$"))
                return textOrMethod;

            var methodName = textOrMethod.Substring(1);
            var type = owner.GetType();

            var method = ReflectionCache.GetMethod(type, methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null || method.ReturnType != typeof(string))
                return null;

            try
            {
                if (method.GetParameters().Length == 0)
                    return (string)method.Invoke(owner, null);
                if (index >= 0 && method.GetParameters().Length == 1)
                    return (string)method.Invoke(owner, new object[] { index });
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to invoke '{methodName}' on {type.Name}: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Разрешает bool-условие по имени метода или свойства на объекте.
        /// Полностью кешировано через ReflectionCache.
        /// </summary>
        internal static bool ResolveBoolCondition(object target, string condition)
        {
            return ReflectionCache.ResolveBoolConditionCached(target, condition);
        }
    }
}
#endif