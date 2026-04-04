using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Десериализация JSON-строки в новые объекты.
    /// </summary>
    public static partial class SerializeController
    {
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
    }
}
