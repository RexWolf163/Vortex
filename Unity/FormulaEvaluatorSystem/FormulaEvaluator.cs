using System;
using System.Collections.Generic;
using System.Reflection;
using Vortex.Unity.FormulaEvaluatorSystem.Model;

namespace Vortex.Unity.FormulaEvaluatorSystem
{
    public static class FormulaEvaluator
    {
        private const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(int), typeof(float), typeof(double), typeof(long),
            typeof(decimal), typeof(byte), typeof(short),
            typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
        };

        private enum MemberKind : byte { Field, Property, Method }

        private readonly struct MemberAccessor
        {
            public readonly MemberKind Kind;
            public readonly FieldInfo Field;
            public readonly PropertyInfo Property;
            public readonly MethodInfo Method;
            public readonly bool IsStatic;

            public MemberAccessor(FieldInfo f)
            {
                Kind = MemberKind.Field;
                Field = f;
                Property = null;
                Method = null;
                IsStatic = f.IsStatic;
            }

            public MemberAccessor(PropertyInfo p)
            {
                Kind = MemberKind.Property;
                Field = null;
                Property = p;
                Method = null;
                IsStatic = p.GetGetMethod(true).IsStatic;
            }

            public MemberAccessor(MethodInfo m)
            {
                Kind = MemberKind.Method;
                Field = null;
                Property = null;
                Method = m;
                IsStatic = m.IsStatic;
            }

            public double Read(object owner)
            {
                var target = IsStatic ? null : owner;
                switch (Kind)
                {
                    case MemberKind.Field: return Convert.ToDouble(Field.GetValue(target));
                    case MemberKind.Property: return Convert.ToDouble(Property.GetValue(target));
                    case MemberKind.Method: return Convert.ToDouble(Method.Invoke(target, null));
                    default: return 0;
                }
            }
        }

        // Cache: (Type, memberName) → accessor
        private static readonly Dictionary<(Type, string), MemberAccessor> AccessorCache =
            new Dictionary<(Type, string), MemberAccessor>();

        /// <summary>
        /// Вычисляет формулу, подставляя значения из привязок slots объекта owner.
        /// </summary>
        public static double Calc(string formula, FormulaSlot[] slots, object owner)
        {
            var parameters = ResolveParameters(slots, owner);
            return FormulaParser.Evaluate(formula, parameters);
        }

        /// <summary>
        /// Безопасная версия Calc.
        /// </summary>
        public static bool TryCalc(string formula, FormulaSlot[] slots, object owner,
            out double result, out string error)
        {
            result = 0;
            error = null;

            try
            {
                var parameters = ResolveParameters(slots, owner);
                return FormulaParser.TryEvaluate(formula, parameters, out result, out error);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static double[] ResolveParameters(FormulaSlot[] slots, object owner)
        {
            if (slots == null || slots.Length == 0)
                return Array.Empty<double>();

            var type = owner.GetType();
            var parameters = new double[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                var name = slots[i].memberName;
                if (string.IsNullOrEmpty(name)) continue;
                parameters[i] = ReadCached(type, owner, name);
            }

            return parameters;
        }

        private static double ReadCached(Type type, object owner, string name)
        {
            var key = (type, name);
            if (!AccessorCache.TryGetValue(key, out var accessor))
            {
                accessor = BuildAccessor(type, name);
                AccessorCache[key] = accessor;
            }

            return accessor.Read(owner);
        }

        private static MemberAccessor BuildAccessor(Type type, string name)
        {
            var field = FindField(type, name);
            if (field != null && IsNumeric(field.FieldType))
                return new MemberAccessor(field);

            var prop = FindProperty(type, name);
            if (prop != null && prop.CanRead && IsNumeric(prop.PropertyType))
                return new MemberAccessor(prop);

            var method = FindMethod(type, name);
            if (method != null && method.GetParameters().Length == 0 && IsNumeric(method.ReturnType))
                return new MemberAccessor(method);

            // Fallback: return a field accessor that will return 0
            throw new InvalidOperationException(
                $"Numeric member '{name}' not found on type '{type.Name}'");
        }

        private static FieldInfo FindField(Type type, string name)
        {
            var result = type.GetField(name, AllMembers);
            if (result != null) return result;

            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                result = baseType.GetField(name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                if (result != null) return result;
                baseType = baseType.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            var result = type.GetProperty(name, AllMembers);
            if (result != null) return result;

            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                result = baseType.GetProperty(name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                if (result != null) return result;
                baseType = baseType.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            var result = type.GetMethod(name, AllMembers, null, Type.EmptyTypes, null);
            if (result != null) return result;

            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                result = baseType.GetMethod(name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (result != null) return result;
                baseType = baseType.BaseType;
            }

            return null;
        }

        private static bool IsNumeric(Type type) => NumericTypes.Contains(type);
    }
}
