#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Elements
{
    internal static class TypeResolver
    {
        private static readonly Dictionary<Type, List<Type>> Cache = new();

        internal static List<Type> GetAssignableTypes(Type baseType)
        {
            if (Cache.TryGetValue(baseType, out var c)) return c;
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => !t.IsAbstract && !t.IsInterface && baseType.IsAssignableFrom(t)
                            && (t.IsSerializable || t.GetCustomAttribute<SerializableAttribute>() != null))
                .OrderBy(t => t.Name).ToList();
            Cache[baseType] = types;
            return types;
        }

        internal static Type GetElementBaseType(FieldInfo fi)
        {
            if (fi == null) return null;
            var ft = fi.FieldType;
            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                return ft.GetGenericArguments()[0];
            if (ft.IsArray) return ft.GetElementType();
            return null;
        }

        internal static bool IsManagedReferenceField(FieldInfo fi)
        {
            if (fi == null) return false;
            if (fi.GetCustomAttribute<SerializeReference>() == null) return false;

            var elementType = GetElementBaseType(fi);
            if (elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                return false;

            return true;
        }

        internal static bool IsElementNull(SerializedProperty el) =>
            el.propertyType == SerializedPropertyType.ManagedReference
            && string.IsNullOrEmpty(el.managedReferenceFullTypename);

        internal static string FormatTypeName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            var sb = new StringBuilder();
            sb.Append(n[0]);
            for (int i = 1; i < n.Length; i++)
            {
                if (char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
                sb.Append(n[i]);
            }
            return sb.ToString();
        }
    }
}
#endif
