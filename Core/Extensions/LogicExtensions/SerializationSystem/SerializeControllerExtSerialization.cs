using System;
using System.Collections;
using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Сериализация объектов в JSON-строку.
    /// </summary>
    public static partial class SerializeController
    {
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
    }
}
