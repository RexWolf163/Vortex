using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>Ответ сервера в том виде, в каком он дошёл до клиента. Создаётся один раз при завершении запроса.</summary>
    public sealed class QueryResult
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>HTTP-код. 0 — ответа не было.</summary>
        public long StatusCode { get; }

        /// <summary>Заголовки ответа (ключи без учёта регистра).</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Тело в памяти. <c>null</c>, если тела нет или ответ сохранён в файл.</summary>
        public byte[] Body { get; }

        /// <summary>Путь к файлу с телом при скачивании в файл. Иначе <c>null</c>.</summary>
        public string FilePath { get; }

        /// <summary>Общая длительность с учётом повторов.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Число выполненных попыток.</summary>
        public int Attempts { get; }

        /// <summary>Диагностическое сообщение (секреты замаскированы). <c>null</c> при успехе.</summary>
        public string ErrorMessage { get; }

        internal QueryResult(long statusCode, IReadOnlyDictionary<string, string> headers, byte[] body, string filePath,
            TimeSpan duration, int attempts, string errorMessage)
        {
            StatusCode = statusCode;
            Headers = headers ?? NoHeaders;
            Body = body;
            FilePath = filePath;
            Duration = duration;
            Attempts = attempts;
            ErrorMessage = errorMessage;
        }

        /// <summary>Есть ли тело (в памяти или в файле).</summary>
        public bool HasBody => (Body != null && Body.Length > 0) || FilePath != null;
    }
}
