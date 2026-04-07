#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vortex.Unity.FormulaEvaluatorSystem
{
    internal enum MemberCategory { Field, Property, Method, Constant }
    internal enum MemberOrigin { Own, Inherited }

    internal struct MemberEntry
    {
        public string Name;
        public MemberCategory Category;
        public MemberOrigin Origin;
        public Type ReturnType;
    }

    internal static class FormulaReflectionResolver
    {
        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(int), typeof(float), typeof(double), typeof(long),
            typeof(decimal), typeof(byte), typeof(short),
            typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
        };

        private static readonly Dictionary<Type, ResolvedMembers> Cache = new Dictionary<Type, ResolvedMembers>();

        internal class ResolvedMembers
        {
            public MemberEntry[] Entries;
            public string[] PopupKeys;
            public string[] MemberNames;
        }

        internal static ResolvedMembers Resolve(Type type)
        {
            if (Cache.TryGetValue(type, out var cached))
                return cached;

            var entries = CollectMembers(type);
            var keys = new string[entries.Count];
            var names = new string[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var category = e.Category == MemberCategory.Constant
                    ? "Constants"
                    : $"{e.Category}s — {(e.Origin == MemberOrigin.Own ? "Own" : "Inherited")}";
                keys[i] = $"{category}/{e.Name} : {e.ReturnType.Name}";
                names[i] = e.Name;
            }

            var result = new ResolvedMembers
            {
                Entries = entries.ToArray(),
                PopupKeys = keys,
                MemberNames = names
            };

            Cache[type] = result;
            return result;
        }

        internal static int FindMemberIndex(string[] memberNames, string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return -1;
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (memberNames[i] == memberName)
                    return i;
            }

            return -1;
        }

        private static List<MemberEntry> CollectMembers(Type type)
        {
            var result = new List<MemberEntry>();
            var seen = new HashSet<string>();

            // Own members (DeclaredOnly)
            CollectFromType(type, MemberOrigin.Own,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly,
                result, seen);

            // Inherited members (walk base types)
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object) && baseType != typeof(UnityEngine.MonoBehaviour)
                   && baseType != typeof(UnityEngine.ScriptableObject))
            {
                CollectFromType(baseType, MemberOrigin.Inherited,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly,
                    result, seen);
                baseType = baseType.BaseType;
            }

            return result;
        }

        private static void CollectFromType(Type type, MemberOrigin origin, BindingFlags flags,
            List<MemberEntry> result, HashSet<string> seen)
        {
            // Fields
            foreach (var field in type.GetFields(flags))
            {
                if (!seen.Add(field.Name)) continue;
                if (!IsNumericType(field.FieldType)) continue;
                // Skip compiler-generated backing fields
                if (field.Name.StartsWith("<")) continue;
                // Inherited private fields are inaccessible
                if (origin == MemberOrigin.Inherited && field.IsPrivate) continue;

                var isConstant = field.IsLiteral || (field.IsStatic && field.IsInitOnly);
                result.Add(new MemberEntry
                {
                    Name = field.Name,
                    Category = isConstant ? MemberCategory.Constant : MemberCategory.Field,
                    Origin = origin,
                    ReturnType = field.FieldType
                });
            }

            // Properties
            foreach (var prop in type.GetProperties(flags))
            {
                if (!seen.Add(prop.Name)) continue;
                if (!prop.CanRead) continue;
                if (!IsNumericType(prop.PropertyType)) continue;
                if (origin == MemberOrigin.Inherited)
                {
                    var getter = prop.GetGetMethod(true);
                    if (getter != null && getter.IsPrivate) continue;
                }

                result.Add(new MemberEntry
                {
                    Name = prop.Name,
                    Category = MemberCategory.Property,
                    Origin = origin,
                    ReturnType = prop.PropertyType
                });
            }

            // Methods (parameterless, numeric return)
            foreach (var method in type.GetMethods(flags))
            {
                if (!seen.Add(method.Name)) continue;
                if (method.GetParameters().Length != 0) continue;
                if (!IsNumericType(method.ReturnType)) continue;
                // Skip property accessors and special methods
                if (method.IsSpecialName) continue;
                if (origin == MemberOrigin.Inherited && method.IsPrivate) continue;

                result.Add(new MemberEntry
                {
                    Name = method.Name,
                    Category = MemberCategory.Method,
                    Origin = origin,
                    ReturnType = method.ReturnType
                });
            }
        }

        private static bool IsNumericType(Type type) => NumericTypes.Contains(type);

        [UnityEditor.InitializeOnLoadMethod]
        private static void ClearCache() => Cache.Clear();
    }
}
#endif
