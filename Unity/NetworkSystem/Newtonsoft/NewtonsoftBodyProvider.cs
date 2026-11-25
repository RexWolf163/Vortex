using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Vortex.Core.NetworkSystem;

namespace Vortex.Unity.NetworkSystem.Providers
{
    /// <summary>
    /// Провайдер тела на Newtonsoft.Json: массивы верхнего уровня, словари, nullable, свойства.
    /// Сборка собирается только при установленном пакете <c>com.unity.nuget.newtonsoft-json</c>;
    /// если пакет удалён, ссылка в ассете пропадает и контроллер падает при инициализации (fail-fast).
    /// Плоская раскладка — по верхнему уровню с теми же настройками имён; вложенный объект — ошибка.
    /// </summary>
    [Serializable]
    public class NewtonsoftBodyProvider : IBodyProvider
    {
        public enum NamingStyle
        {
            AsIs,
            CamelCase,
            SnakeCase
        }

        [SerializeField, Tooltip("Стиль имён полей в JSON.")]
        private NamingStyle naming = NamingStyle.AsIs;

        [SerializeField, Tooltip("Не писать поля со значением null.")]
        private bool ignoreNulls;

        [NonSerialized] private JsonSerializerSettings _settings;

        public string ContentType => "application/json; charset=utf-8";

        public byte[] Serialize(object value) =>
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Settings));

        public bool TryDeserialize(byte[] data, Type type, out object value, out string error)
        {
            try
            {
                value = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), type, Settings);
                error = value == null ? "Пустой JSON." : null;
                return value != null;
            }
            catch (Exception e)
            {
                value = null;
                error = e.Message;
                return false;
            }
        }

        public bool CanFlatten => true;

        public IReadOnlyList<KeyValuePair<string, object>> Flatten(object value)
        {
            var result = new List<KeyValuePair<string, object>>();
            var root = JObject.FromObject(value, JsonSerializer.Create(Settings));

            foreach (var property in root.Properties())
            {
                switch (property.Value)
                {
                    case JValue scalar:
                        if (scalar.Value != null)
                            result.Add(new KeyValuePair<string, object>(property.Name, scalar.Value));
                        break;

                    case JArray array:
                        foreach (var item in array)
                        {
                            if (item is not JValue element)
                                throw NotFlat(value, property.Name);
                            if (element.Value != null)
                                result.Add(new KeyValuePair<string, object>(property.Name, element.Value));
                        }

                        break;

                    default:
                        throw NotFlat(value, property.Name);
                }
            }

            return result;
        }

        private JsonSerializerSettings Settings => _settings ??= new JsonSerializerSettings
        {
            NullValueHandling = ignoreNulls ? NullValueHandling.Ignore : NullValueHandling.Include,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = naming switch
                {
                    NamingStyle.CamelCase => new CamelCaseNamingStrategy(),
                    NamingStyle.SnakeCase => new SnakeCaseNamingStrategy(),
                    _ => new DefaultNamingStrategy()
                }
            }
        };

        private static Exception NotFlat(object value, string property) =>
            new InvalidOperationException(
                $"[Network] {value.GetType().Name}.{property} is a nested object and cannot be flattened into URL parameters.");
    }
}
