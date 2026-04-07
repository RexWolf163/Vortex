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
                parameters[i] = ReadNumericMember(type, owner, name);
            }

            return parameters;
        }

        private static double ReadNumericMember(Type type, object owner, string name)
        {
            // Field
            var field = FindField(type, name);
            if (field != null && IsNumeric(field.FieldType))
                return Convert.ToDouble(field.GetValue(field.IsStatic ? null : owner));

            // Property
            var prop = FindProperty(type, name);
            if (prop != null && prop.CanRead && IsNumeric(prop.PropertyType))
            {
                var getter = prop.GetGetMethod(true);
                return Convert.ToDouble(prop.GetValue(getter.IsStatic ? null : owner));
            }

            // Method
            var method = FindMethod(type, name);
            if (method != null && method.GetParameters().Length == 0 && IsNumeric(method.ReturnType))
                return Convert.ToDouble(method.Invoke(method.IsStatic ? null : owner, null));

            return 0;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            var result = type.GetField(name, AllMembers);
            if (result != null) return result;

            // Walk base types for private members (FlattenHierarchy doesn't include them)
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
