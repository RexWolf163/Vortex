using System;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>Правила адресов сетевого модуля, общие для контроллера и драйверов.</summary>
    public static class NetworkUrl
    {
        /// <summary>
        /// Нормализация для проверки масок: <c>схема://хост[:порт]/путь</c> без параметров; схема и хост в нижнем
        /// регистре, порт по умолчанию убран, регистр пути сохранён.
        /// </summary>
        public static string NormalizeForMasks(Uri uri) =>
            uri.Scheme.ToLowerInvariant() + "://" + uri.Host.ToLowerInvariant()
            + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port)
            + uri.AbsolutePath;

        /// <summary>То же для строки адреса. Невалидный адрес — исключение.</summary>
        public static string NormalizeForMasks(string url) =>
            NormalizeForMasks(new Uri(url, UriKind.Absolute));
    }
}
