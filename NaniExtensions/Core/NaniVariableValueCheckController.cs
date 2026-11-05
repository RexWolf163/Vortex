using System;
using System.Globalization;
using Naninovel;
using UnityEngine;

namespace Vortex.NaniExtensions.Core
{
    /// <summary>
    /// Сравнение значения переменной Naninovel с целью-строкой: число — с оператором (<c>&gt;=3</c>, <c>&lt;1</c>,
    /// <c>=2</c>, без оператора — равенство), логическое — <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>, строка — равенство
    /// без учёта регистра. Общее для условий, логик квестов и UI-переключателей по переменным.
    /// </summary>
    public static class NaniVariableValueCheckController
    {
        public static bool Check(this CustomVariableValue variable, string targetValue)
        {
            // Invariant обязателен: культурная ToLower() на турецкой локали даёт I → 'ı' (dotless),
            // после чего Equals(..., OrdinalIgnoreCase) ниже перестаёт совпадать с исходным значением.
            targetValue = targetValue.Trim().ToLowerInvariant();
            var result = false;
            switch (variable.Type)
            {
                case CustomVariableValueType.String:
                    result = variable.String.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
                    break;
                case CustomVariableValueType.Numeric:
                {
                    var t = targetValue;
                    string op;
                    string num;

                    // Ordinal: разбор оператора — это синтаксис переменной, а не текст. StartsWith(string)
                    // по умолчанию КУЛЬТУРНЫЙ, и его правила сопоставления не должны влиять на парсинг.
                    if      (t.StartsWith(">=", StringComparison.Ordinal)) { op = ">="; num = t[2..]; }
                    else if (t.StartsWith("<=", StringComparison.Ordinal)) { op = "<="; num = t[2..]; }
                    else if (t.StartsWith("==", StringComparison.Ordinal)) { op = "=="; num = t[2..]; }
                    else if (t.StartsWith(">",  StringComparison.Ordinal)) { op = ">";  num = t[1..]; }
                    else if (t.StartsWith("<",  StringComparison.Ordinal)) { op = "<";  num = t[1..]; }
                    else if (t.StartsWith("=",  StringComparison.Ordinal)) { op = "=="; num = t[1..]; }
                    else                                                   { op = "=="; num = t;     }

                    var rhs = float.Parse(num.Trim(), CultureInfo.InvariantCulture);
                    result = op switch
                    {
                        "==" => Mathf.Approximately(variable.Number, rhs),
                        ">=" => variable.Number >= rhs,
                        "<=" => variable.Number <= rhs,
                        ">"  => variable.Number >  rhs,
                        "<"  => variable.Number <  rhs,
                        _    => false
                    };
                    break;
                }
                case CustomVariableValueType.Boolean:
                    result = variable.Boolean
                        ? targetValue is "true" or "1"
                        : targetValue is "false" or "0" or "";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return result;
        }
    }
}
