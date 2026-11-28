using System.Collections.Generic;
using System.Linq;
using Vortex.Core.NetworkSystem.Model.Enums;

namespace Vortex.Core.NetworkSystem.Driver
{
    /// <summary>
    /// Всё, что нужно драйверу на одну попытку: номер запроса и попытки, итоговый URL, метод, заголовки,
    /// сегменты тела, приёмник ответа (память или путь), лимит размера ответа, запрет переходов.
    /// </summary>
    public sealed class PreparedAttempt
    {
        private readonly Dictionary<string, string> _headers;
        private readonly BodySegment[] _body;

        /// <summary>Номер запроса в сессии.</summary>
        public int QueryId { get; }

        /// <summary>Номер попытки запроса, с 1.</summary>
        public int AttemptNumber { get; }

        /// <summary>Итоговый URL.</summary>
        public string Url { get; }

        /// <summary>HTTP-метод.</summary>
        public QueryMethod Method { get; }

        /// <summary>Заголовки запроса; имена — без учёта регистра.</summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;

        /// <summary>Сегменты тела в порядке отправки. Пусто — тела нет.</summary>
        public IReadOnlyList<BodySegment> Body => _body;

        /// <summary>
        /// Путь к файлу-приёмнику ответа. <c>null</c> — ответ в память. Файл появляется по этому пути только
        /// после сохранения (<see cref="INetworkAttempt.SaveResponseFile"/>).
        /// </summary>
        public string ResponseFilePath { get; }

        /// <summary>Лимит размера ответа, байты.</summary>
        public long ResponseSizeLimit { get; }

        /// <summary>Запрет следовать редиректам: ответ 3xx возвращается как есть.</summary>
        public bool ForbidRedirects { get; }

        /// <summary>Создать подготовленную попытку.</summary>
        /// <param name="queryId">Номер запроса в сессии.</param>
        /// <param name="attemptNumber">Номер попытки, с 1.</param>
        /// <param name="url">Итоговый URL.</param>
        /// <param name="method">HTTP-метод.</param>
        /// <param name="headers">Заголовки; копируются.</param>
        /// <param name="body">Сегменты тела; список копируется.</param>
        /// <param name="responseFilePath">Путь к файлу-приёмнику или <c>null</c>.</param>
        /// <param name="responseSizeLimit">Лимит размера ответа, байты.</param>
        /// <param name="forbidRedirects">Запрет следовать редиректам.</param>
        internal PreparedAttempt(int queryId, int attemptNumber, string url, QueryMethod method,
            IEnumerable<KeyValuePair<string, string>> headers, IEnumerable<BodySegment> body,
            string responseFilePath, long responseSizeLimit, bool forbidRedirects)
        {
            QueryId = queryId;
            AttemptNumber = attemptNumber;
            Url = url;
            Method = method;
            _headers = NetworkTools.CopyHeaders(headers);
            _body = body.ToArray();
            ResponseFilePath = responseFilePath;
            ResponseSizeLimit = responseSizeLimit;
            ForbidRedirects = forbidRedirects;
        }
    }
}
