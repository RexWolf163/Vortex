using System;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;

namespace Vortex.Core.LocalizationSystem
{
    public static class StringExt
    {
        /// <summary>
        /// Возвращает ассоциацию с ключом в текущей локали приложения
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Translate(this String key) =>
            key.IsNullOrWhitespace() ? "" : Localization.GetTranslate(key);

        /// <summary>
        /// Возвращает ассоциацию с ключом в текущей локали приложения
        /// Если ассоциации нет, то ключ без изменений
        /// Можно применять для специфических случаев. Например для индикации названия систем,
        /// которые без перевода должны быть выведены как есть
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string TryTranslate(this String key) =>
            Localization.HasTranslate(key) ? Localization.GetTranslate(key) : key;
    }
}