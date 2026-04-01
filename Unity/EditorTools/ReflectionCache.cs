#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vortex.Unity.EditorTools
{
    /// <summary>
    /// Централизованный кеш для reflection-операций.
    /// Устраняет повторные вызовы Type.GetField/GetMethod/GetCustomAttributes
    /// на каждый Repaint инспектора.
    /// </summary>
    internal static class ReflectionCache
    {
        // ════════════════════════════════════════════════════════
        //  FieldInfo cache
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<(Type, string), FieldInfo> FieldCache = new();

        internal static FieldInfo GetField(Type type, string name, BindingFlags flags)
        {
            var key = (type, name);
            if (FieldCache.TryGetValue(key, out var cached))
                return cached;

            var field = type.GetField(name, flags);
            FieldCache[key] = field;
            return field;
        }

        /// <summary>
        /// GetField с обходом базовых классов (как в MoveElementDirect).
        /// </summary>
        internal static FieldInfo GetFieldWithBase(Type type, string name, BindingFlags flags)
        {
            var field = GetField(type, name, flags);
            if (field != null) return field;

            var t = type.BaseType;
            while (t != null)
            {
                field = GetField(t, name, flags);
                if (field != null) return field;
                t = t.BaseType;
            }

            return null;
        }

        // ════════════════════════════════════════════════════════
        //  MethodInfo cache
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<(Type, string), MethodInfo> MethodCache = new();

        internal static MethodInfo GetMethod(Type type, string name, BindingFlags flags)
        {
            var key = (type, name);
            if (MethodCache.TryGetValue(key, out var cached))
                return cached;

            var method = type.GetMethod(name, flags);
            MethodCache[key] = method;
            return method;
        }

        internal static MethodInfo GetMethodNoArgs(Type type, string name, BindingFlags flags)
        {
            var key = (type, name);
            if (MethodCache.TryGetValue(key, out var cached))
                return cached;

            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            MethodCache[key] = method;
            return method;
        }

        // ════════════════════════════════════════════════════════
        //  PropertyInfo cache
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<(Type, string), PropertyInfo> PropertyCache = new();

        internal static PropertyInfo GetProperty(Type type, string name, BindingFlags flags)
        {
            var key = (type, name);
            if (PropertyCache.TryGetValue(key, out var cached))
                return cached;

            var prop = type.GetProperty(name, flags);
            PropertyCache[key] = prop;
            return prop;
        }

        // ════════════════════════════════════════════════════════
        //  Custom Attributes cache (per FieldInfo)
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<FieldInfo, object[]> AttributesCache = new();

        internal static object[] GetCustomAttributes(FieldInfo fieldInfo, bool inherit)
        {
            if (AttributesCache.TryGetValue(fieldInfo, out var cached))
                return cached;

            var attrs = fieldInfo.GetCustomAttributes(inherit);
            AttributesCache[fieldInfo] = attrs;
            return attrs;
        }

        // ════════════════════════════════════════════════════════
        //  FieldInfo resolution by SerializedProperty path
        //  (кеш результатов ToolsController.GetFieldInfo)
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<(Type, string), FieldInfo> PropertyPathFieldCache = new();

        internal static bool TryGetFieldInfoByPath(Type rootType, string propertyPath, out FieldInfo result)
        {
            var key = (rootType, propertyPath);
            return PropertyPathFieldCache.TryGetValue(key, out result);
        }

        internal static void CacheFieldInfoByPath(Type rootType, string propertyPath, FieldInfo fieldInfo)
        {
            PropertyPathFieldCache[(rootType, propertyPath)] = fieldInfo;
        }

        // ════════════════════════════════════════════════════════
        //  Bool condition resolution cache
        //  (кеш MemberInfo для ResolveBoolCondition)
        // ════════════════════════════════════════════════════════

        private enum BoolMemberKind : byte { None, Property, Method, Field }

        private readonly struct BoolMemberEntry
        {
            public readonly BoolMemberKind Kind;
            public readonly MemberInfo Member;

            public BoolMemberEntry(BoolMemberKind kind, MemberInfo member)
            {
                Kind = kind;
                Member = member;
            }
        }

        private static readonly Dictionary<(Type, string), BoolMemberEntry> BoolConditionCache = new();

        internal static bool ResolveBoolConditionCached(object target, string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            var type = target.GetType();
            var key = (type, condition);

            if (!BoolConditionCache.TryGetValue(key, out var entry))
            {
                const BindingFlags flags =
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var prop = type.GetProperty(condition, flags);
                if (prop != null && prop.CanRead && prop.PropertyType == typeof(bool))
                {
                    entry = new BoolMemberEntry(BoolMemberKind.Property, prop);
                }
                else
                {
                    var method = type.GetMethod(condition, flags, null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType == typeof(bool))
                    {
                        entry = new BoolMemberEntry(BoolMemberKind.Method, method);
                    }
                    else
                    {
                        var field = type.GetField(condition, flags);
                        if (field != null && field.FieldType == typeof(bool))
                            entry = new BoolMemberEntry(BoolMemberKind.Field, field);
                        else
                            entry = new BoolMemberEntry(BoolMemberKind.None, null);
                    }
                }

                BoolConditionCache[key] = entry;
            }

            return entry.Kind switch
            {
                BoolMemberKind.Property => (bool)((PropertyInfo)entry.Member).GetValue(target),
                BoolMemberKind.Method => (bool)((MethodInfo)entry.Member).Invoke(target, null),
                BoolMemberKind.Field => (bool)((FieldInfo)entry.Member).GetValue(target),
                _ => false
            };
        }

        // ════════════════════════════════════════════════════════
        //  ManagedReference type resolution cache
        // ════════════════════════════════════════════════════════

        private static readonly Dictionary<string, Type> ManagedReferenceTypeCache = new();

        internal static Type ResolveManagedReferenceType(string managedReferenceFullTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFullTypename))
                return null;

            if (ManagedReferenceTypeCache.TryGetValue(managedReferenceFullTypename, out var cached))
                return cached;

            var spaceIndex = managedReferenceFullTypename.IndexOf(' ');
            if (spaceIndex < 0)
            {
                ManagedReferenceTypeCache[managedReferenceFullTypename] = null;
                return null;
            }

            var assemblyName = managedReferenceFullTypename.Substring(0, spaceIndex);
            var className = managedReferenceFullTypename.Substring(spaceIndex + 1);

            Type resolved = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != assemblyName) continue;
                resolved = asm.GetType(className);
                if (resolved != null) break;
            }

            ManagedReferenceTypeCache[managedReferenceFullTypename] = resolved;
            return resolved;
        }

        // ════════════════════════════════════════════════════════
        //  Clear (на случай domain reload)
        // ════════════════════════════════════════════════════════

        [UnityEditor.InitializeOnLoadMethod]
        private static void SubscribeToDomainReload()
        {
            // Кеши static, переживут recompile через domain reload.
            // Unity обнулит их автоматически при полном reload,
            // но на всякий случай — чистим явно.
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += Clear;
        }

        internal static void Clear()
        {
            FieldCache.Clear();
            MethodCache.Clear();
            PropertyCache.Clear();
            AttributesCache.Clear();
            PropertyPathFieldCache.Clear();
            BoolConditionCache.Clear();
            ManagedReferenceTypeCache.Clear();
        }
    }
}
#endif