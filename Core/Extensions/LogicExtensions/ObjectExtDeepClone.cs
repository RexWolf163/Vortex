using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ObjectExtDeepClone
    {
        private static readonly Dictionary<Type, FieldInfo[]> FieldsCache = new();

        /// <summary>
        /// Создает глубокую копию объекта.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="returnOriginalOnError"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T DeepCopy<T>(this T source, bool returnOriginalOnError = false)
        {
            if (ReferenceEquals(source, null))
                return default;

            var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            return (T)CopyInternal(source, visited, returnOriginalOnError);
        }

        private static object CopyInternal(object obj, Dictionary<object, object> visited, bool returnOriginalOnError)
        {
            if (obj == null)
                return null;

            var type = obj.GetType();

            // primitive / immutable types
            if (IsPrimitive(type))
                return obj;

            if (GetPlatformPrimitives().Contains(type))
                return obj;

            // cycle check
            if (visited.TryGetValue(obj, out var existing))
                return existing;

            // arrays
            if (type.IsArray)
                return CopyArray((Array)obj, visited, returnOriginalOnError);

            // dictionaries
            if (typeof(IDictionary).IsAssignableFrom(type))
                return CopyDictionary((IDictionary)obj, visited, returnOriginalOnError);

            // lists / collections
            if (typeof(IList).IsAssignableFrom(type))
                return CopyList((IList)obj, visited, returnOriginalOnError);

            // ICloneable support. Должен создавать Deep Clone!
            if (obj is ICloneable cloneable)
            {
                visited.Add(obj, null);
                var c = cloneable.Clone();
                visited[obj] = c;
                return c;
            }

            // create instance
            object copy;
            try
            {
                copy = Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                if (!returnOriginalOnError)
                {
                    Log.Print(LogLevel.Error, $"DeepCopy failed for {type.Name}: {e.Message}.",
                        obj);
                    return null;
                }

                Log.Print(LogLevel.Common, $"DeepCopy failed for {type.Name}: {e.Message}. Pointer was return.",
                    obj);
                return obj;
            }

            visited[obj] = copy;

            // copy fields
            if (!FieldsCache.TryGetValue(type, out var fields))
            {
                var allFields = new List<FieldInfo>();

                for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                {
                    allFields.AddRange(t.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly));
                }

                fields = allFields.ToArray();
                FieldsCache[type] = fields;
            }

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                field.SetValue(copy, CopyInternal(value, visited, returnOriginalOnError));
            }

            return copy;
        }

        private static object CopyArray(Array array, Dictionary<object, object> visited, bool returnOriginalOnError)
        {
            var elementType = array.GetType().GetElementType();
            var clone = Array.CreateInstance(elementType, array.Length);

            visited[array] = clone;

            for (int i = 0; i < array.Length; i++)
            {
                clone.SetValue(CopyInternal(array.GetValue(i), visited, returnOriginalOnError), i);
            }

            return clone;
        }

        private static object CopyList(IList list, Dictionary<object, object> visited, bool returnOriginalOnError)
        {
            IList copy;
            try
            {
                copy = (IList)Activator.CreateInstance(list.GetType());
            }
            catch (Exception e)
            {
                if (!returnOriginalOnError)
                {
                    Log.Print(LogLevel.Error,
                        $"DeepCopy failed for {list.GetType().Name}: {e.Message}.",
                        list);
                    return null;
                }

                Log.Print(LogLevel.Common,
                    $"DeepCopy failed for {list.GetType().Name}: {e.Message}. Pointer was return.",
                    list);

                return list;
            }

            visited[list] = copy;

            foreach (var item in list)
            {
                copy.Add(CopyInternal(item, visited, returnOriginalOnError));
            }

            return copy;
        }

        private static object CopyDictionary(IDictionary dict, Dictionary<object, object> visited,
            bool returnOriginalOnError)
        {
            IDictionary copy;
            try
            {
                copy = (IDictionary)Activator.CreateInstance(dict.GetType());
            }
            catch (Exception e)
            {
                if (!returnOriginalOnError)
                {
                    Log.Print(LogLevel.Error,
                        $"DeepCopy failed for {dict.GetType().Name}: {e.Message}.",
                        dict);
                    return null;
                }

                Log.Print(LogLevel.Common,
                    $"DeepCopy failed for {dict.GetType().Name}: {e.Message}. Pointer was return.",
                    dict);

                return dict;
            }

            visited[dict] = copy;

            foreach (DictionaryEntry entry in dict)
            {
                var key = CopyInternal(entry.Key, visited, returnOriginalOnError);
                var value = CopyInternal(entry.Value, visited, returnOriginalOnError);

                copy.Add(key, value);
            }

            return copy;
        }

        private static bool IsPrimitive(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type == typeof(Uri) ||
                   type == typeof(Version) ||
                   type.IsEnum;
        }

        private static Type[] GetPlatformPrimitives() =>
            typeof(SimpleTypeMarker).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.FieldType)
                .ToArray();
    }

    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}