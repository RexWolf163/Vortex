using System;
using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Внутренние служебные операции пакета: fast-fail (лог ошибки и возврат исключения) и копирование
    /// заголовков в словарь с именами без учёта регистра (повторы имени склеиваются).
    /// </summary>
    internal static class NetworkTools
    {
        /// <summary>
        /// Fast-fail: записать сообщение исключения в лог как ошибку и вернуть исключение для <c>throw</c>.
        /// </summary>
        /// <param name="exception">Исключение с читаемым сообщением.</param>
        /// <param name="source">Имя типа, сообщающего об ошибке.</param>
        internal static Exception Fail(Exception exception, string source)
        {
            Log.Print(LogLevel.Error, exception.Message, source);
            return exception;
        }

        /// <summary>
        /// Копия заголовков в новом словаре; имена — без учёта регистра. Повтор имени склеивается через
        /// «, » (RFC 9110, п. 5.3): значения не теряются. Для <c>Set-Cookie</c> такая склейка неразборчива —
        /// куки модуль не разбирает.
        /// </summary>
        internal static Dictionary<string, string> CopyHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
                copy[header.Key] = copy.TryGetValue(header.Key, out var present)
                    ? present + ", " + header.Value
                    : header.Value;
            return copy;
        }
    }
}
