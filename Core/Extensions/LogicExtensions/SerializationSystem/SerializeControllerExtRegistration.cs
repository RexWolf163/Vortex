using System;
using System.Collections.Generic;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Регистрация custom-конвертеров для типов, не подпадающих под стандартные правила
    /// <c>IsSimpleType</c> и атрибута <c>[POCO]</c>. Регистрируемое семейство типов ведёт себя
    /// как simple-тип: сериализуется в JSON-строку, десериализуется обратно через предоставленные
    /// функции.
    ///
    /// Применение — типы, чьи singleton-инстансы нельзя пересоздавать через <c>Activator.CreateInstance</c>
    /// (например, type-safe enum-подобные классы), но которые имеют стабильное строковое представление.
    /// </summary>
    public static partial class SerializeController
    {
        private static readonly List<CustomConverter> _customConverters = new();

        private readonly struct CustomConverter
        {
            public readonly Func<Type, bool> Matches;
            public readonly Func<object, string> Serialize;
            public readonly Func<Type, string, object> Deserialize;

            public CustomConverter(Func<Type, bool> matches,
                Func<object, string> serialize,
                Func<Type, string, object> deserialize)
            {
                Matches = matches;
                Serialize = serialize;
                Deserialize = deserialize;
            }
        }

        /// <summary>
        /// Регистрирует custom-конвертер для семейства типов.
        /// </summary>
        /// <param name="matches">Предикат: применим ли конвертер к данному Type.</param>
        /// <param name="serialize">Функция сериализации в строку (без обрамляющих кавычек).</param>
        /// <param name="deserialize">Функция десериализации: на вход тип-приёмник и строка (без кавычек).</param>
        /// <remarks>
        /// Регистрация идемпотентна по комбинации трёх делегатов — повторный вызов с теми же ссылками
        /// не дублирует запись. Это позволяет безопасно регистрировать из static-инициализатора
        /// при возможных повторных входах (домен-релоад, перезагрузка плагинов).
        /// </remarks>
        public static void RegisterCustomSerializer(
            Func<Type, bool> matches,
            Func<object, string> serialize,
            Func<Type, string, object> deserialize)
        {
            if (matches == null || serialize == null || deserialize == null) return;

            foreach (var c in _customConverters)
            {
                if (ReferenceEquals(c.Matches, matches)
                    && ReferenceEquals(c.Serialize, serialize)
                    && ReferenceEquals(c.Deserialize, deserialize))
                    return;
            }

            _customConverters.Add(new CustomConverter(matches, serialize, deserialize));
        }

        /// <summary>
        /// Поиск конвертера по типу. Первый зарегистрированный совпадающий выигрывает.
        /// </summary>
        private static bool TryGetCustomConverter(Type type, out CustomConverter converter)
        {
            if (type != null)
            {
                foreach (var c in _customConverters)
                {
                    if (c.Matches(type))
                    {
                        converter = c;
                        return true;
                    }
                }
            }

            converter = default;
            return false;
        }
    }
}
