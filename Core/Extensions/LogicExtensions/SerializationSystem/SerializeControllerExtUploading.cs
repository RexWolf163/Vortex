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
    /// Загрузка десериализованных данных в существующие объекты.
    /// В отличие от DeserializeProperties, не создаёт новые экземпляры,
    /// а обновляет переданные, сохраняя ключи словарей, которых нет в данных.
    /// </summary>
    public static partial class SerializeController
    {
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
            var type = target.GetType();
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
                            if (value == null)
                            {
                                props.SetValue(target, DeserializeClass(propsType, valueText));
                            }
                            else
                            {
                                UploadDictionary(value, valueText);
                            }

                            continue;
                        }

                        if (propsType.IsArray)
                        {
                            UploadArray(ref value, valueText);
                            props.SetValue(target, value);
                            continue;
                        }

                        //прочие коллекции
                        if (propsType != typeof(string) && typeof(IList).IsAssignableFrom(propsType))
                        {
                            if (value == null)
                            {
                                props.SetValue(target, DeserializeClass(propsType, valueText));
                            }
                            else
                            {
                                UploadCollection(value, valueText);
                            }

                            continue;
                        }

                        if (value == null)
                        {
                            props.SetValue(target, DeserializeClass(propsType, valueText));
                        }
                        else
                        {
                            UploadClass(value, valueText);
                            props.SetValue(target, value);
                        }
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
            var type = target.GetType();
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

            data = TrimData(data);
            var ar = SeparateText(data);

            var targetDict = (IDictionary)target;
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
    }
}
