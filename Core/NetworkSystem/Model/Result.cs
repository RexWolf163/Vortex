using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>
    /// Ответ сервера, как он дошёл до клиента: HTTP-код, заголовки, тело (байты или путь к файлу),
    /// общая длительность, число попыток, диагностическое сообщение.
    /// </summary>
    public sealed class Result
    {
        private readonly Dictionary<string, string> _headers;

        /// <summary>HTTP-код. <c>0</c> — ответа не было.</summary>
        public int StatusCode { get; }

        /// <summary>Заголовки ответа; имена — без учёта регистра.</summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;

        /// <summary>Тело в памяти. <c>null</c>, если тела нет или оно в файле. Массив не копируется — не изменять.</summary>
        public byte[] Bytes { get; }

        /// <summary>Путь к файлу с телом. <c>null</c>, если тела нет или оно в памяти.</summary>
        public string FilePath { get; }

        /// <summary>Общая длительность с учётом повторов, секунды.</summary>
        public float Duration { get; }

        /// <summary>Число выполненных попыток.</summary>
        public int Attempts { get; }

        /// <summary>Диагностическое сообщение; секреты в нём уже замаскированы.</summary>
        public string Message { get; }

        /// <summary>Создать ответ.</summary>
        /// <param name="statusCode">HTTP-код, <c>0</c> — ответа не было.</param>
        /// <param name="headers">Заголовки ответа; копируются.</param>
        /// <param name="bytes">Тело в памяти или <c>null</c>.</param>
        /// <param name="filePath">Путь к файлу с телом или <c>null</c>.</param>
        /// <param name="duration">Общая длительность, секунды.</param>
        /// <param name="attempts">Число попыток.</param>
        /// <param name="message">Диагностическое сообщение с замаскированными секретами.</param>
        internal Result(int statusCode, IEnumerable<KeyValuePair<string, string>> headers, byte[] bytes,
            string filePath, float duration, int attempts, string message)
        {
            StatusCode = statusCode;
            _headers = NetworkTools.CopyHeaders(headers);
            Bytes = bytes;
            FilePath = filePath;
            Duration = duration;
            Attempts = attempts;
            Message = message;
        }
    }
}
