using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Vortex.Core.NetworkSystem;

namespace Vortex.Unity.NetworkSystem.Providers
{
    /// <summary>
    /// Провайдер тела на <see cref="JsonUtility"/> (по умолчанию). Правила JsonUtility: публичные поля и поля с
    /// <c>[SerializeField]</c>, без свойств и словарей, без массива верхнего уровня.
    /// Плоская раскладка — по тем же полям: скаляры, коллекции скаляров (повторяющимися ключами); вложенный объект — ошибка.
    /// </summary>
    [Serializable]
    public class JsonUtilityBodyProvider : IBodyProvider
    {
        public string ContentType => "application/json; charset=utf-8";

        public byte[] Serialize(object value) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));

        public bool TryDeserialize(byte[] data, Type type, out object value, out string error)
        {
            try
            {
                value = JsonUtility.FromJson(Encoding.UTF8.GetString(data), type);
                error = value == null ? "JsonUtility вернул пустой объект." : null;
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
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in value.GetType().GetFields(flags))
            {
                var serialized = field.IsPublic
                    ? !field.IsDefined(typeof(NonSerializedAttribute))
                    : field.IsDefined(typeof(SerializeField));
                if (!serialized)
                    continue;

                var fieldValue = field.GetValue(value);
                if (fieldValue == null)
                    continue;

                if (IsScalar(fieldValue.GetType()))
                {
                    result.Add(new KeyValuePair<string, object>(field.Name, fieldValue));
                    continue;
                }

                if (fieldValue is IEnumerable items)
                {
                    foreach (var item in items)
                    {
                        if (item == null)
                            continue;
                        if (!IsScalar(item.GetType()))
                            throw NotFlat(value, field.Name);
                        result.Add(new KeyValuePair<string, object>(field.Name, item));
                    }

                    continue;
                }

                throw NotFlat(value, field.Name);
            }

            return result;
        }

        private static bool IsScalar(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset);

        private static Exception NotFlat(object value, string field) =>
            new InvalidOperationException(
                $"[Network] {value.GetType().Name}.{field} is a nested object and cannot be flattened into URL parameters.");
    }
}
