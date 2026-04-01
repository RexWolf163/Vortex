using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ObjectExtDeepClone
    {
        private static readonly Dictionary<Type, FieldInfo[]> FieldsCache = new();

        public static T DeepCopy<T>(this T source, bool givePointerOnError = false)
        {
            if (ReferenceEquals(source, null))
                return default;

            var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            return (T)CopyInternal(source, visited, givePointerOnError);
        }

        private static object CopyInternal(object obj, Dictionary<object, object> visited, bool givePointerOnError)
        {
            if (obj == null)
                return null;

            var type = obj.GetType();

            // primitive / immutable types
            if (IsPrimitive(type))
                return obj;

            // cycle check
            if (visited.TryGetValue(obj, out var existing))
                return existing;

            // arrays
            if (type.IsArray)
                return CopyArray((Array)obj, visited, givePointerOnError);

            // lists / collections
            if (typeof(IList).IsAssignableFrom(type))
                return CopyList((IList)obj, visited, givePointerOnError);

            // dictionaries
            if (typeof(IDictionary).IsAssignableFrom(type))
                return CopyDictionary((IDictionary)obj, visited, givePointerOnError);

            // ICloneable support
            if (obj is ICloneable cloneable)
                return cloneable.Clone();

            // create instance
            object copy;
            try
            {
                copy = Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                if (!givePointerOnError)
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
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldsCache[type] = fields;
            }

            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue; // readonly skip

                var value = field.GetValue(obj);
                field.SetValue(copy, CopyInternal(value, visited, givePointerOnError));
            }

            return copy;
        }

        private static object CopyArray(Array array, Dictionary<object, object> visited, bool givePointerOnError)
        {
            var elementType = array.GetType().GetElementType();
            var clone = Array.CreateInstance(elementType, array.Length);

            visited[array] = clone;

            for (int i = 0; i < array.Length; i++)
            {
                clone.SetValue(CopyInternal(array.GetValue(i), visited, givePointerOnError), i);
            }

            return clone;
        }

        private static object CopyList(IList list, Dictionary<object, object> visited, bool givePointerOnError)
        {
            IList copy;
            try
            {
                copy = (IList)Activator.CreateInstance(list.GetType());
            }
            catch (Exception e)
            {
                if (!givePointerOnError)
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
                copy.Add(CopyInternal(item, visited, givePointerOnError));
            }

            return copy;
        }

        private static object CopyDictionary(IDictionary dict, Dictionary<object, object> visited,
            bool givePointerOnError)
        {
            IDictionary copy;
            try
            {
                copy = (IDictionary)Activator.CreateInstance(dict.GetType());
            }
            catch (Exception e)
            {
                if (!givePointerOnError)
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
                var key = CopyInternal(entry.Key, visited, givePointerOnError);
                var value = CopyInternal(entry.Value, visited, givePointerOnError);

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
    }
}