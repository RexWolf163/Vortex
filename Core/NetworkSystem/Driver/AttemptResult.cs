using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem.Driver
{
    /// <summary>
    /// Что вернула попытка: HTTP-код, заголовки, тело (байты или путь к файлу) либо категория ошибки
    /// и сообщение транспорта. Ровно одно из двух: HTTP-ответ или ошибка.
    /// </summary>
    public sealed class AttemptResult
    {
        private readonly Dictionary<string, string> _headers;

        /// <summary>HTTP-код. <c>0</c> — HTTP-ответа не было.</summary>
        public int StatusCode { get; }

        /// <summary>Заголовки ответа; имена — без учёта регистра. Пусто, если ответа не было.</summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;

        /// <summary>Тело в памяти. <c>null</c>, если тела нет или оно в файле. Массив не копируется — не изменять.</summary>
        public byte[] Bytes { get; }

        /// <summary>Путь к файлу с телом. <c>null</c>, если тела нет или оно в памяти.</summary>
        public string FilePath { get; }

        /// <summary>Почему нет HTTP-ответа. <see cref="AttemptError.None"/> — ответ получен.</summary>
        public AttemptError Error { get; }

        /// <summary>Сообщение транспорта об ошибке. <c>null</c>, если ответ получен.</summary>
        public string Message { get; }

        private AttemptResult(int statusCode, IEnumerable<KeyValuePair<string, string>> headers, byte[] bytes,
            string filePath, AttemptError error, string message)
        {
            StatusCode = statusCode;
            _headers = NetworkTools.CopyHeaders(headers);
            Bytes = bytes;
            FilePath = filePath;
            Error = error;
            Message = message;
        }

        /// <summary>HTTP-ответ получен. Код не больше 0 — ошибка программиста (fast-fail).</summary>
        /// <param name="statusCode">HTTP-код, больше 0.</param>
        /// <param name="headers">Заголовки ответа; копируются.</param>
        /// <param name="bytes">Тело в памяти или <c>null</c>.</param>
        /// <param name="filePath">Путь к файлу с телом или <c>null</c>.</param>
        public static AttemptResult Response(int statusCode, IEnumerable<KeyValuePair<string, string>> headers,
            byte[] bytes, string filePath)
        {
            if (statusCode <= 0)
                throw NetworkTools.Fail(new ArgumentOutOfRangeException(nameof(statusCode), statusCode,
                    "HTTP-ответ попытки без HTTP-кода: для ошибки без ответа — Failed."), nameof(AttemptResult));
            return new AttemptResult(statusCode, headers, bytes, filePath, AttemptError.None, null);
        }

        /// <summary>
        /// HTTP-ответа нет: попытка закончилась ошибкой. <see cref="AttemptError.None"/> — ошибка программиста
        /// (fast-fail).
        /// </summary>
        /// <param name="error">Категория ошибки, не <see cref="AttemptError.None"/>.</param>
        /// <param name="message">Сообщение транспорта.</param>
        public static AttemptResult Failed(AttemptError error, string message)
        {
            if (error == AttemptError.None)
                throw NetworkTools.Fail(new ArgumentException(
                    "Ошибка попытки без категории: для полученного ответа — Response.", nameof(error)),
                    nameof(AttemptResult));
            return new AttemptResult(0, Array.Empty<KeyValuePair<string, string>>(), null, null, error, message);
        }
    }
}
