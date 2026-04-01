using System;
using System.Collections.Generic;

namespace Vortex.Core.Extensions.LogicExtensions
{
    /// <summary>
    /// Фабрика для упрощенного создания индексов
    /// </summary>
    public static class IndexFabric
    {
        /// <summary>
        /// Создает словарь с настройкой StringComparer.InvariantCultureIgnoreCase
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Dictionary<string, T> Create<T>()
            => new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Создает словарь с настройкой StringComparer.InvariantCultureIgnoreCase
        /// и заполняет из источника source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Dictionary<string, T> Create<T>(IDictionary<string, T> source)
            => new(source, StringComparer.InvariantCultureIgnoreCase);
    }
}