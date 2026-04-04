using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Контроллер сериализации и десериализации.
    /// Обрабатывает публичные свойства с 'public' геттером и любым сеттером.
    ///
    /// Сложные типы сериализуются только если помечены атрибутом [POCO].
    /// Простые типы (примитивы, string, enum, DateTime, Guid) — всегда.
    /// Свойства с атрибутом [NotPOCO] исключаются из сериализации.
    ///
    /// Если в нескольких свойствах содержится указатель на одну сущность,
    /// при сериализации выдаст ошибку (защита от зацикливания).
    /// </summary>
    public static partial class SerializeController
    {
        #region Parameters

        /// <summary>
        /// Кеширование сериализуемых свойств (с учётом [NotPOCO] и IsSerializableType)
        /// </summary>
        private static readonly Dictionary<Type, PropertyInfo[]> CacheFields = new();

        /// <summary>
        /// Кеш проверки [POCO] атрибута на типах
        /// </summary>
        private static readonly Dictionary<Type, bool> CachePOCO = new();

        private static readonly HashSet<object> VisitedObjects = new();

        private const string ClassTypeField = "__";

        #endregion

        #region POCO Validation

        /// <summary>
        /// Проверяет наличие атрибута [POCO] на типе или его интерфейсах (с кешированием)
        /// </summary>
        private static bool IsPOCO(Type type)
        {
            if (type == null) return false;
            if (!CachePOCO.TryGetValue(type, out var result))
            {
                result = type.GetCustomAttribute<POCOAttribute>() != null
                         || type.GetInterfaces().Any(i => i.GetCustomAttribute<POCOAttribute>() != null);
                CachePOCO[type] = result;
            }

            return result;
        }

        /// <summary>
        /// Определяет, может ли тип быть сериализован.
        /// Простые типы — всегда. Коллекции — если элемент сериализуем. Остальные — только с [POCO].
        /// </summary>
        private static bool IsSerializableType(Type type)
        {
            if (type == null) return false;
            if (IsSimpleType(type)) return true;

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var args = type.GetGenericArguments();
                if (args.Length < 2) return false;
                if (!IsSimpleType(args[0]) && args[0] != typeof(Type)) return false;
                return IsSerializableType(args[1]);
            }

            if (type.IsArray)
                return IsSerializableType(type.GetElementType());

            if (typeof(IList).IsAssignableFrom(type) && type != typeof(string))
            {
                var args = type.GetGenericArguments();
                return args.Length > 0 && IsSerializableType(args[0]);
            }

            return IsPOCO(type);
        }

        #endregion

        #region Common

        /// <summary>
        /// Проверка на принадлежность к "простым" типам данных
        /// </summary>
        private static bool IsSimpleType(Type type)
        {
            if (type == null) return true;

            if (type.IsPrimitive) return true;
            if (type.IsEnum) return true;

            if (type == typeof(string)) return true;
            if (type == typeof(decimal)) return true;
            if (type == typeof(DateTime)) return true;
            if (type == typeof(Guid)) return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsSimpleType(type.GetGenericArguments()[0]);

            return false;
        }

        /// <summary>
        /// Возвращает список свойств подлежащих сериализации.
        /// Фильтрует: публичный getter, наличие setter, отсутствие [NotPOCO], тип сериализуем.
        /// </summary>
        private static PropertyInfo[] GetReadablePropertiesList(Type type)
        {
            if (!CacheFields.TryGetValue(type, out var props))
            {
                props = type
                    .GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead
                                && p.SetMethod != null
                                && p.GetCustomAttribute<NotPOCOAttribute>() == null
                                && IsSerializableType(p.PropertyType))
                    .ToArray();
                CacheFields[type] = props;
            }

            return props;
        }

        private static string JsonEncode(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string JsonDecode(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

        /// <summary>
        /// Разделитель текста по блокам.
        /// Считает закрывающие и открывающие скобки, а так же кавычки
        /// </summary>
        private static List<string> SeparateText(string data)
        {
            var result = new List<string>();

            var inQuotes = false;
            var bracket = 0;
            var lastPoint = 0;
            for (var i = 0; i < data.Length; i++)
            {
                switch (inQuotes)
                {
                    case false when data[i] == '"':
                        inQuotes = true;
                        continue;
                    case true when data[i] == '"' && data[i - 1] != '\\':
                        inQuotes = false;
                        continue;
                    case true:
                        continue;
                }

                switch (data[i])
                {
                    case '{':
                    case '[':
                        bracket++;
                        continue;
                    case '}':
                    case ']':
                        bracket--;
                        if (bracket < 0)
                        {
                            Log.Print(LogLevel.Error, $"Deserialization error for «{data}»", "SerializeController");
                            return null;
                        }

                        continue;
                }

                if (bracket != 0 || data[i] != ',' || lastPoint == i)
                    continue;
                result.Add(data.Substring(lastPoint, i - lastPoint));
                lastPoint = i + 1;
            }

            if (lastPoint < data.Length)
                result.Add(data[lastPoint..]);
            return result;
        }

        private static string TakeQuotesText(this string text, out string endPart)
        {
            text = text.TrimStart('\t', '\n', '\r', ' ');
            var l = text.Length;
            endPart = String.Empty;
            if (l < 2)
                return text;
            var end = l;
            var endPartStart = l;
            var wasDivider = false;
            //Нет кавычек - значит специальное значение
            if (text[0] != '"')
                return text;

            for (var i = 1; i < l; i++)
            {
                var c = text[i];
                if (end != l && text[i] == ':')
                {
                    wasDivider = true;
                    continue;
                }

                if (wasDivider && text[i] == ' ')
                {
                    endPartStart = i;
                    break;
                }

                if (c != '"' || text[i - 1] == '\\')
                    continue;
                if (end == l)
                    end = i;
            }

            //странная ситуация. скорее всего ошибка данных
            if (end < 0)
                return text;

            endPart = text.Substring(endPartStart, l - endPartStart);
            endPart = endPart.Trim(' ', '\t', '\n', '\r');
            return text.Substring(1, end - 1);
        }

        private static string TrimData(string data, bool isArray = false)
        {
            var openChar = isArray ? '[' : '{';
            var closeChar = isArray ? ']' : '}';

            data = data.Trim(' ', '\n', '\r', '\t');
            if (data.Length == 0)
                return "";
            var first = -1;
            var c = data.Length;
            for (int i = 0; i < c; i++)
            {
                var ch = data[i];
                if (ch != openChar)
                    continue;
                first = i + 1;
                break;
            }

            var last = c;
            for (int i = c - 1; i >= 0; i--)
            {
                var ch = data[i];
                if (ch != closeChar)
                    continue;
                last = i - 1;
                break;
            }


            if (first >= 0)
                return data.Substring(first, last - first);
            Log.Print(LogLevel.Error, $"Char {openChar} not found", "SerializeController");
            return null;
        }

        /// <summary>
        /// Заполняет простой тип строковыми данными.
        /// Поддерживает все типы, возвращающие true в IsSimpleType.
        /// </summary>
        private static object SetSimple(Type type, string valueStr)
        {
            if (valueStr == "null") return null;

            valueStr = valueStr.Trim('\t', '\n', '\r', ' ');

            // Убираем внешние кавычки точечно (первая и последняя), не Trim
            if (valueStr.Length >= 2 && valueStr[0] == '"' && valueStr[^1] == '"')
                valueStr = valueStr[1..^1];

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return SetSimple(type.GetGenericArguments()[0], valueStr);

            if (type == typeof(Guid))
                return Guid.Parse(valueStr);

            if (type.IsEnum)
                return Enum.Parse(type, valueStr, ignoreCase: true);

            var typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.String:
                    return JsonDecode(valueStr);
                case TypeCode.Boolean:
                    return bool.Parse(valueStr);
                case TypeCode.Int32:
                    return int.Parse(valueStr);
                case TypeCode.Int64:
                    return long.Parse(valueStr);
                case TypeCode.Int16:
                    return short.Parse(valueStr);
                case TypeCode.Byte:
                    return byte.Parse(valueStr);
                case TypeCode.SByte:
                    return sbyte.Parse(valueStr);
                case TypeCode.UInt32:
                    return uint.Parse(valueStr);
                case TypeCode.UInt64:
                    return ulong.Parse(valueStr);
                case TypeCode.UInt16:
                    return ushort.Parse(valueStr);
                case TypeCode.Single:
                    return float.Parse(valueStr, CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return double.Parse(valueStr, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return decimal.Parse(valueStr, CultureInfo.InvariantCulture);
                case TypeCode.Char:
                    return valueStr.Length > 0 ? valueStr[0] : '\0';
                case TypeCode.DateTime:
                {
                    if (DateTime.TryParseExact(valueStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var dt))
                        return dt;
                    Log.Print(LogLevel.Error, $"Unknown DateTime value \"{valueStr}\"", "SerializeController");
                    return null;
                }
            }

            Log.Print(LogLevel.Error, $"Simple type {type.FullName} is not supported", "SerializeController");
            return null;
        }

        private static string GetSimple(object model)
        {
            if (model == null) return "null";
            var type = model.GetType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var innerType = type.GetGenericArguments()[0];
                return GetSimple(Convert.ChangeType(model, innerType));
            }

            if (type == typeof(Guid))
                return $"\"{model}\"";

            if (type.IsEnum)
                return $"\"{model}\"";

            var typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.Char:
                    return $"\"{model}\"";
                case TypeCode.String:
                    return $"\"{JsonEncode((string)model)}\"";
                case TypeCode.Boolean:
                    return (bool)model ? "true" : "false";
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Int16:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.UInt16:
                    return $"{model}";
                case TypeCode.Single:
                    return ((float)model).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return ((double)model).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return ((decimal)model).ToString(CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return ((DateTime)model).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            throw new NotSupportedException($"Unsupported simple type: {type.FullName}");
        }

        #endregion
    }
}
