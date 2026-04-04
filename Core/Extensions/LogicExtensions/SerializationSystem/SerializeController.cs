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
    public static class SerializeController
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

        #region Serialization

        /// <summary>
        /// Сериализует модель в строку JSON.
        /// Сериализуются только публичные свойства с getter и setter.
        /// Сложные типы должны быть помечены [POCO], иначе пропускаются.
        /// </summary>
        public static string SerializeProperties(this object model)
        {
            if (model == null)
                return string.Empty;
            try
            {
                VisitedObjects.Clear();
                return model.SerializeClass();
            }
            finally
            {
                VisitedObjects.Clear();
            }
        }

        private static string SerializeClass(this Object model, int deep = 0)
        {
            var tabs = new string(' ', 2 * deep++);
            var tabsChilds = new string(' ', 2 * deep);
            var type = model?.GetType();
            if (IsSimpleType(type))
                return GetSimple(model);

            if (!VisitedObjects.Add(model))
            {
                Log.Print(LogLevel.Error, "Serialization failed from cycled model data", model);
                return String.Empty;
            }

            var ar = new List<string>();
            var isArray = false;

            //словари
            if (typeof(IDictionary).IsAssignableFrom(type))
                ar = SerializeDictionary(model, deep);
            //прочие коллекции
            else if (type != typeof(string) && typeof(IList).IsAssignableFrom(type))
            {
                isArray = true;
                ar = SerializeArray(model, deep);
            }
            else
            {
                if (!IsPOCO(type))
                {
                    if (Settings.Data().DebugMode)
                        Log.Print(LogLevel.Warning,
                            $"Type {type?.Name} is not marked [POCO], skipping serialization",
                            "SerializeController");
                    return "null";
                }

                var props = GetReadablePropertiesList(type);
                ar.Add($"\"{ClassTypeField}\" : \"{type.AssemblyQualifiedName}\"");
                foreach (var prop in props)
                {
                    var value = prop.GetValue(model);
                    var str = $"\"{prop.Name}\" : {value.SerializeClass(deep)}";
                    ar.Add(str);
                }
            }

            var serialized = string.Join($",\n{tabsChilds}", ar);
            return isArray
                ? $"[\n{tabsChilds}{serialized}\n{tabs}]"
                : $"{{\n{tabsChilds}{serialized}\n{tabs}}}";
        }

        private static List<string> SerializeDictionary(object model, int deep = 0)
        {
            var ar = new List<string>();
            var dict = model as IDictionary;
            if (dict == null)
            {
                Log.Print(LogLevel.Error, $"Serialization error for {model}", model);
                return null;
            }

            var genericParams = dict.GetType().GetGenericArguments();
            if (!IsSimpleType(genericParams[0]) && genericParams[0] != typeof(Type))
            {
                Log.Print(LogLevel.Error, "Serialization for classes in dictionary key not supported", model);
                return null;
            }

            foreach (var key in dict.Keys)
            {
                var item = dict[key];
                var serializedKey = genericParams[0] == typeof(Type)
                    ? $"\"{((Type)key).AssemblyQualifiedName}\""
                    : GetSimple(key);

                ar.Add($"{serializedKey} : {item.SerializeClass(deep)}");
            }

            return ar;
        }

        private static List<string> SerializeArray(object model, int deep = 0)
        {
            var ar = new List<string>();
            var collection = model as IList;
            if (collection == null)
            {
                Log.Print(LogLevel.Error, $"Serialization error for {model}", model);
                return null;
            }

            foreach (var item in collection)
                ar.Add($"{item.SerializeClass(deep)}");
            return ar;
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

        #region Deserialization

        /// <summary>
        /// Десериализатор данных из строки JSON выполненной сериализацией этого же контроллера.
        /// Десериализуются только публичные свойства с getter и setter.
        /// Сложные типы должны быть помечены [POCO].
        /// </summary>
        public static T DeserializeProperties<T>(this string data)
        {
            var type = typeof(T);
            try
            {
                var result = (T)type.DeserializeClass(data);
                return result;
            }
            catch (Exception ex)
            {
                Log.Print(LogLevel.Error, $"{ex.Message}\n{ex.StackTrace}", "SerializeController");
                return default;
            }
        }

        private static object DeserializeClass(this Type type, string data)
        {
            if (IsSimpleType(type))
                return SetSimple(type, data);

            if (data == "null")
                return null;

            //словари
            if (typeof(IDictionary).IsAssignableFrom(type))
                return DeserializeDictionary(type, data);

            if (type.IsArray)
                return DeserializeArray(type, data);

            //прочие коллекции
            if (type != typeof(string) && typeof(IList).IsAssignableFrom(type))
                return DeserializeCollection(type, data);


            data = TrimData(data);
            if (string.IsNullOrEmpty(data))
                return null;
            var ar = SeparateText(data);
            var classId = TakeQuotesText(ar[0], out var typeName);
            typeName = typeName.Trim('"');
            if (classId != "__")
            {
                Log.Print(LogLevel.Error, $"Class ID field missed", "SerializeController");
                return null;
            }

            var typeId = Type.GetType(typeName);

            if (typeId == null)
            {
                Log.Print(LogLevel.Error, $"Type not found for deserialization: {typeName}", "SerializeController");
                return null;
            }

            if (!type.IsAssignableFrom(typeId))
            {
                Log.Print(LogLevel.Error, $"Wrong type family: {typeName}", "SerializeController");
                return null;
            }

            type = typeId;

            if (!IsPOCO(type))
            {
                if (Settings.Data().DebugMode)
                    Log.Print(LogLevel.Warning,
                        $"Type {type.Name} is not marked [POCO], skipping deserialization",
                        "SerializeController");
                return null;
            }

            var model = Activator.CreateInstance(type);
            if (model == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return null;
            }

            var c = ar.Count;
            for (var i = 1; i < c; i++)
            {
                var s = ar[i];
                var name = TakeQuotesText(s, out var valueText);
                var props = type.GetProperty(name);
                if (props == null)
                {
                    if (Settings.Data().DebugMode)
                        Log.Print(LogLevel.Warning,
                            $"Property {type.Name}.{name} not found, skipping",
                            "SerializeController");
                    continue;
                }

                if (props.GetCustomAttribute<NotPOCOAttribute>() != null)
                    continue;

                if (props.SetMethod != null)
                {
                    props.SetValue(model, DeserializeClass(props.PropertyType, valueText));
                }
                else
                {
                    if (Settings.Data().DebugMode)
                        Log.Print(LogLevel.Warning,
                            $"Property {type.Name}.{name} has no setter, skipping",
                            "SerializeController");
                }
            }

            return model;
        }

        private static object DeserializeCollection(Type type, string data)
        {
            var elementType = type.GenericTypeArguments[0];
            if (elementType == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return null;
            }

            var list = Activator.CreateInstance(type) as IList;
            if (list == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return null;
            }

            data = TrimData(data, true);
            var ar = SeparateText(data);
            foreach (var s in ar)
            {
                var element = DeserializeClass(elementType, s);
                if (element == null)
                    continue;
                list.Add(element);
            }

            return list;
        }

        private static object DeserializeDictionary(Type type, string data)
        {
            Type keyType = null;
            Type valueType = null;

            var dictGenericInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictGenericInterface != null)
            {
                var args = dictGenericInterface.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
            }
            else
            {
                Log.Print(LogLevel.Error, $"Unsupported dictionary type for deserialization: {type}", type);
                return null;
            }

            if (!IsSimpleType(keyType) && keyType != typeof(Type))
            {
                Log.Print(LogLevel.Error, $"Unsupported dictionary type for deserialization: {type}", type);
                return null;
            }

            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dict = (IDictionary)Activator.CreateInstance(dictType);

            data = TrimData(data);
            var ar = SeparateText(data);

            foreach (var s in ar)
            {
                var name = TakeQuotesText(s, out var valueText);
                var key = keyType == typeof(Type) ? Type.GetType(name) : SetSimple(keyType, name);
                if (key != null)
                {
                    dict.Add(key, DeserializeClass(valueType, valueText));
                    continue;
                }

                Log.Print(LogLevel.Error, $"Error deserialization for key: {name}", type);
            }

            return dict;
        }

        private static object DeserializeArray(Type type, string data)
        {
            data = TrimData(data, true);
            var ar = SeparateText(data);
            var elementType = type.GetElementType();
            if (elementType == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return null;
            }

            var list = Array.CreateInstance(elementType, ar.Count);

            for (var i = ar.Count - 1; i >= 0; i--)
            {
                var s = ar[i];
                var item = DeserializeClass(elementType, s);
                list.SetValue(item, i);
            }

            return list;
        }

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

        #endregion

        #region Upload

        /// <summary>
        /// Подгружает десериализуемые данные из строки JSON выполненной сериализацией этого же контроллера,
        /// в указанный объект.
        /// Десериализуются только публичные свойства с getter и setter.
        /// Сложные типы должны быть помечены [POCO].
        /// </summary>
        public static void UploadProperties<T>(this string data, T target) where T : class
        {
            try
            {
                var type = typeof(T);
                //словари
                if (typeof(IDictionary).IsAssignableFrom(type))
                {
                    UploadDictionary(target, data);
                    return;
                }

                if (type.IsArray)
                {
                    UploadArray(ref target, data);
                    return;
                }

                //прочие коллекции
                if (type != typeof(string) && typeof(IList).IsAssignableFrom(type))
                {
                    UploadCollection(target, data);
                    return;
                }

                UploadClass(target, data);
            }
            catch (Exception ex)
            {
                Log.Print(LogLevel.Error, $"{ex.Message}\n{ex.StackTrace}", "SerializeController");
            }
        }

        private static void ClearClass<T>(T target) where T : class
        {
            var type = typeof(T);
            var properties = GetReadablePropertiesList(type);
            foreach (var prop in properties)
            {
                if (prop == null)
                    continue;

                if (prop.SetMethod != null)
                {
                    var propsType = prop.PropertyType;
                    if (IsSimpleType(propsType))
                    {
                        prop.SetValue(target, default);
                        continue;
                    }

                    //словари
                    if (typeof(IDictionary).IsAssignableFrom(propsType))
                    {
                        prop.SetValue(target, null);
                        continue;
                    }

                    if (propsType.IsArray)
                    {
                        prop.SetValue(target, null);
                        continue;
                    }

                    //прочие коллекции
                    if (propsType != typeof(string) && typeof(IList).IsAssignableFrom(propsType))
                    {
                        prop.SetValue(target, null);
                        continue;
                    }

                    prop.SetValue(target, null);
                }
            }
        }

        private static void UploadClass<T>(this T target, string data) where T : class
        {
            data = TrimData(data);
            if (string.IsNullOrEmpty(data))
            {
                ClearClass(target);
                return;
            }

            var ar = SeparateText(data);
            var classId = TakeQuotesText(ar[0], out var typeName);
            typeName = typeName.Trim('"');
            if (classId != "__")
            {
                Log.Print(LogLevel.Error, $"Class ID field missed", "SerializeController");
                return;
            }

            var typeId = Type.GetType(typeName);

            if (typeId == null)
            {
                Log.Print(LogLevel.Error, $"Type not found for deserialization: {typeName}", "SerializeController");
                return;
            }

            var type = target.GetType();
            if (!type.IsAssignableFrom(typeId))
            {
                Log.Print(LogLevel.Error, $"Wrong type family: {typeName}", "SerializeController");
                return;
            }

            type = typeId;

            if (!IsPOCO(type))
            {
                if (Settings.Data().DebugMode)
                    Log.Print(LogLevel.Warning,
                        $"Type {type.Name} is not marked [POCO], skipping deserialization",
                        "SerializeController");
                return;
            }

            try
            {
                var c = ar.Count;
                for (var i = 1; i < c; i++)
                {
                    var s = ar[i];
                    var name = TakeQuotesText(s, out var valueText);
                    var props = type.GetProperty(name);
                    if (props == null)
                    {
                        if (Settings.Data().DebugMode)
                            Log.Print(LogLevel.Warning,
                                $"Property {type.Name}.{name} not found, skipping",
                                "SerializeController");
                        continue;
                    }

                    if (props.GetCustomAttribute<NotPOCOAttribute>() != null)
                        continue;

                    if (props.SetMethod != null)
                    {
                        var value = props.GetValue(target);
                        var propsType = props.PropertyType;
                        if (IsSimpleType(propsType))
                        {
                            value = SetSimple(propsType, valueText);
                            props.SetValue(target, value);
                            continue;
                        }

                        //словари
                        if (typeof(IDictionary).IsAssignableFrom(propsType))
                        {
                            UploadDictionary(value, valueText);
                            continue;
                        }

                        if (propsType.IsArray)
                        {
                            UploadArray(ref value, valueText);
                            continue;
                        }

                        //прочие коллекции
                        if (propsType != typeof(string) && typeof(IList).IsAssignableFrom(propsType))
                        {
                            UploadCollection(value, valueText);
                            continue;
                        }

                        UploadClass(value, valueText);
                        props.SetValue(target, value);
                    }
                    else
                    {
                        if (Settings.Data().DebugMode)
                            Log.Print(LogLevel.Warning,
                                $"Property {type.Name}.{name} has no setter, skipping",
                                "SerializeController");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Print(LogLevel.Error, $"{ex.Message}\n{ex.StackTrace}", "SerializeController");
            }
        }

        private static void UploadCollection<T>(T target, string data)
        {
            var type = typeof(T);
            var elementType = type.GenericTypeArguments[0];
            if (elementType == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return;
            }

            var list = target as IList;
            if (list == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return;
            }

            list.Clear();

            data = TrimData(data, true);
            var ar = SeparateText(data);
            foreach (var s in ar)
            {
                var element = DeserializeClass(elementType, s);
                if (element == null)
                    continue;
                list.Add(element);
            }
        }

        private static void UploadDictionary<T>(T target, string data)
        {
            var type = target.GetType();
            Type keyType = null;
            Type valueType = null;

            var dictGenericInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (dictGenericInterface != null)
            {
                var args = dictGenericInterface.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
            }
            else
            {
                Log.Print(LogLevel.Error, $"Unsupported dictionary type for deserialization: {type}", type);
                return;
            }

            if (!IsSimpleType(keyType) && keyType != typeof(Type))
            {
                Log.Print(LogLevel.Error, $"Unsupported dictionary type for deserialization: {type}", type);
                return;
            }

            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dict = (IDictionary)Activator.CreateInstance(dictType);

            data = TrimData(data);
            var ar = SeparateText(data);

            var targetDict = (IDictionary)target;
            var listKeys = new List<object>();
            foreach (var s in ar)
            {
                var name = TakeQuotesText(s, out var valueText);
                var key = keyType == typeof(Type) ? Type.GetType(name) : SetSimple(keyType, name);
                if (key != null)
                {
                    if (!targetDict.Contains(key))
                        targetDict.Add(key, DeserializeClass(valueType, valueText));
                    else
                    {
                        var value = targetDict[key];
                        UploadClass(value, valueText);
                        targetDict[key] = value;
                    }

                    listKeys.Add(key);
                    continue;
                }

                Log.Print(LogLevel.Error, $"Error deserialization for key: {name}", type);
            }
        }

        private static void UploadArray<T>(ref T target, string data)
        {
            var type = target.GetType();
            data = TrimData(data, true);
            var ar = SeparateText(data);
            var elementType = type.GetElementType();
            if (elementType == null)
            {
                Log.Print(LogLevel.Error, $"Deserialization error for {type}", type);
                return;
            }

            var list = Array.CreateInstance(elementType, ar.Count);

            for (var i = ar.Count - 1; i >= 0; i--)
            {
                var s = ar[i];
                var item = DeserializeClass(elementType, s);
                list.SetValue(item, i);
            }

            target = (T)(object)list;
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

        #endregion
    }
}