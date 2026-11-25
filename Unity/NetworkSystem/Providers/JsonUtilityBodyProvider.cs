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

        /// <summary>
        /// Только объект с сериализуемыми полями. На null, скаляре, строке, массиве, списке, а также на типе без полей
        /// по правилам JsonUtility (DTO на свойствах, анонимный тип, Guid, DateTime) JsonUtility молча отдаёт пустой
        /// результат или «{}» — это ошибка программиста, а не пустое тело.
        /// </summary>
        public byte[] Serialize(object value)
        {
            if (value == null || IsScalar(value.GetType()) || value is IEnumerable || !HasSerializedFields(value.GetType()))
                throw new InvalidOperationException(
                    $"[Network] JsonUtility cannot serialize {(value == null ? "null" : value.GetType().Name)} as a body: " +
                    "it needs a class with public or [SerializeField] fields. Wrap the value or use NewtonsoftBodyProvider.");
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));
        }

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
            foreach (var field in SerializableFieldCandidates(value.GetType()))
            {
                if (!IsSerialized(field))
                    continue; // в теле JsonUtility этого поля тоже нет — раскладка по тем же полям

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

        /// <summary>
        /// Поля типа и всех его предков, от базового к производному (как у JsonUtility): GetFields не возвращает
        /// приватные поля базовых классов, а [SerializeField] на них JsonUtility сериализует.
        /// </summary>
        private static IEnumerable<FieldInfo> SerializableFieldCandidates(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.DeclaredOnly;
            var hierarchy = new Stack<Type>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                hierarchy.Push(current);

            while (hierarchy.Count > 0)
                foreach (var field in hierarchy.Pop().GetFields(flags))
                    yield return field;
        }

        private static bool HasSerializedFields(Type type)
        {
            foreach (var field in SerializableFieldCandidates(type))
                if (IsSerialized(field))
                    return true;
            return false;
        }

        /// <summary>
        /// Поле попадает в JSON JsonUtility: публичное без [NonSerialized] или с [SerializeField], не readonly,
        /// и тип поддерживается (Nullable, decimal, DateTime, DateTimeOffset JsonUtility пропускает).
        /// </summary>
        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsInitOnly || field.IsLiteral)
                return false;
            var marked = field.IsPublic
                ? !field.IsDefined(typeof(NonSerializedAttribute))
                : field.IsDefined(typeof(SerializeField));
            if (!marked)
                return false;
            var type = field.FieldType;
            return Nullable.GetUnderlyingType(type) == null && type != typeof(decimal)
                                                            && type != typeof(DateTime) && type != typeof(DateTimeOffset);
        }

        private static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string);

        private static Exception NotFlat(object value, string field) =>
            new InvalidOperationException(
                $"[Network] {value.GetType().Name}.{field} is a nested object and cannot be flattened into URL parameters.");
    }
}
