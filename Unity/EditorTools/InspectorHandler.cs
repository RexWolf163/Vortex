#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Vortex.Unity.EditorTools
{
    internal static class InspectorHandler
    {
        private const BindingFlags AllInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Навигация по propertyPath для получения объекта-владельца поля.
        /// </summary>
        internal static object GetFieldOwner(SerializedProperty property)
        {
            try
            {
                var path = property.propertyPath;
                object obj = property.serializedObject.targetObject;
                if (obj is null)
                    return null;

                var type = obj.GetType();

                var parts = path.Replace(".Array.data[", ".[").Split('.');

                // Идём до предпоследнего элемента — он и есть владелец
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (parts[i].StartsWith("["))
                    {
                        // Индекс массива/списка
                        var indexStr = parts[i].Trim('[', ']');
                        if (int.TryParse(indexStr, out int index))
                        {
                            if (obj is IList list && index < list.Count)
                            {
                                obj = list[index];
                                type = obj?.GetType();
                            }
                            else return null;
                        }
                    }
                    else
                    {
                        var field = ReflectionCache.GetField(type, parts[i], AllInstance);
                        if (field == null) return null;
                        obj = field.GetValue(obj);
                        type = field.FieldType;
                    }

                    if (obj == null) return null;
                }

                return obj;
            }
            catch (ObjectDisposedException ex)
            {
                //Ignore
            }

            return null;
        }

        public static bool IsPropertyNullable(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                case SerializedPropertyType.ObjectReference:
                    return true;

                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.AnimationCurve:
                case SerializedPropertyType.Gradient:
                case SerializedPropertyType.FixedBufferSize:
                case SerializedPropertyType.LayerMask:
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempts to get the actual managed reference object that this property represents.
        /// </summary>
        internal static object GetValueOfProperty(this SerializedProperty property)
        {
            var so = property.serializedObject;
            if (so == null) return null;

            var targetObject = so.targetObject;
            if (targetObject == null) return null;

            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            object currentValue = targetObject;

            foreach (var element in elements)
            {
                if (currentValue == null) return null;

                string elementName = element;
                int arrayIndex = -1;

                if (element.Contains("["))
                {
                    int startBracket = element.IndexOf('[');
                    elementName = element.Substring(0, startBracket);
                    string indexStr = element.Substring(startBracket + 1, element.Length - startBracket - 2);
                    if (!int.TryParse(indexStr, out arrayIndex)) return null;
                }

                FieldInfo fieldInfo = ReflectionCache.GetField(
                    currentValue.GetType(), elementName, AllInstance);
                if (fieldInfo == null) return null;

                currentValue = fieldInfo.GetValue(currentValue);
                if (currentValue == null) return null;

                if (arrayIndex >= 0)
                {
                    if (currentValue is IList list)
                    {
                        if (arrayIndex >= 0 && arrayIndex < list.Count)
                            currentValue = list[arrayIndex];
                        else
                            return null;
                    }
                    else return null;
                }
            }

            return currentValue;
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

        internal static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            var rootType = property.serializedObject.targetObject.GetType();
            var propertyPath = property.propertyPath;

            // Проверяем кеш по (rootType, propertyPath)
            /*
            if (ReflectionCache.TryGetFieldInfoByPath(rootType, propertyPath, out var cached))
                return cached;
                */

            // Вычисляем и кешируем
            var result = ResolveFieldInfo(rootType, propertyPath, property.serializedObject);
            ReflectionCache.CacheFieldInfoByPath(rootType, propertyPath, result);
            return result;
        }

        private static FieldInfo ResolveFieldInfo(Type rootType, string propertyPath, SerializedObject serializedObject)
        {
            var path = propertyPath.Replace(".Array.data[", ".[").Split('.');
            var type = rootType;
            FieldInfo lastField = null;

            for (int i = 0; i < path.Length; i++)
            {
                if (path[i].StartsWith("["))
                {
                    if (type.IsArray)
                        type = type.GetElementType();
                    else if (type.IsGenericType)
                        type = type.GetGenericArguments()[0];
                    else
                        return null;

                    // Элемент массива — последний сегмент: возвращаем FieldInfo массива
                    if (i == path.Length - 1)
                        return lastField;

                    continue;
                }

                var field = ReflectionCache.GetFieldWithBase(type, path[i], AllInstance);

                // SerializeReference: объявленный тип может не содержать поля —
                // пробуем runtime-тип через managedReferenceFullTypename
                if (field == null && serializedObject != null)
                {
                    var resolvedType = TryResolveManagedReferenceType(serializedObject, propertyPath, path, i);
                    if (resolvedType != null)
                    {
                        field = ReflectionCache.GetFieldWithBase(resolvedType, path[i], AllInstance);
                        type = resolvedType;
                    }
                }

                if (field == null) return null;

                lastField = field;

                if (i == path.Length - 1)
                    return field;

                type = field.FieldType;
            }

            return null;
        }

        /// <summary>
        /// Для поля внутри SerializeReference: восстанавливает путь до родительского property,
        /// проверяет managedReferenceFullTypename и возвращает runtime-тип.
        /// </summary>
        private static Type TryResolveManagedReferenceType(SerializedObject serializedObject,
            string fullPath, string[] pathSegments, int currentIndex)
        {
            var parentPath = BuildParentPath(fullPath, pathSegments, currentIndex);
            if (parentPath == null) return null;

            var parentProp = serializedObject.FindProperty(parentPath);
            if (parentProp == null || parentProp.propertyType != SerializedPropertyType.ManagedReference)
                return null;

            var typeName = parentProp.managedReferenceFullTypename;
            return ReflectionCache.ResolveManagedReferenceType(typeName);
        }

        /// <summary>
        /// Восстанавливает оригинальный propertyPath до сегмента currentIndex
        /// из нормализованных pathSegments (где ".Array.data[" заменён на ".[").
        /// </summary>
        private static string BuildParentPath(string fullPath, string[] pathSegments, int currentIndex)
        {
            var pos = 0;
            for (int i = 0; i < currentIndex; i++)
            {
                var segment = pathSegments[i];
                if (segment.StartsWith("["))
                {
                    // "[N]" в segments = ".Array.data[N]" в fullPath
                    var arrayStart = fullPath.IndexOf(".Array.data[", pos, StringComparison.Ordinal);
                    if (arrayStart < 0) return null;
                    var bracket = fullPath.IndexOf(']', arrayStart);
                    if (bracket < 0) return null;
                    pos = bracket + 1;
                }
                else
                {
                    if (pos > 0 && pos < fullPath.Length && fullPath[pos] == '.')
                        pos++;
                    pos += segment.Length;
                }
            }

            if (pos <= 0 || pos > fullPath.Length) return null;
            return fullPath.Substring(0, pos);
        }

        public static Type GetCollectionElementType(FieldInfo field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            Type fieldType = field.FieldType;

            if (fieldType.IsArray)
                return fieldType.GetElementType();

            var enumerableInterface = fieldType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                     i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableInterface != null)
                return enumerableInterface.GetGenericArguments()[0];

            if (typeof(IEnumerable).IsAssignableFrom(fieldType))
                return typeof(object);

            return null;
        }
    }
}

#endif