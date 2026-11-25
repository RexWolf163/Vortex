using System;
using System.Collections.Generic;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Драйвер сети — прокладка к транспорту без логики. Превращает подготовленную попытку в объект попытки и объявляет
    /// возможности. Таймаут, отмену, наблюдение за прогрессом, повторы и освобождение ведёт контроллер.
    /// До вызова <see cref="ISystemDriver.OnInit"/> драйвер обязан выставить <see cref="Settings"/>.
    /// </summary>
    public interface INetworkDriver : ISystemDriver
    {
        /// <summary>Настройки связи (ассет драйвера). Читаются контроллером при инициализации.</summary>
        INetworkSettings Settings { get; }

        /// <summary>Подсказка о доступности сети. Запросы не блокирует.</summary>
        bool IsReachable { get; }

        /// <summary>Возможность: скачивание ответа в файл.</summary>
        bool SupportsDownloadToFile { get; }

        /// <summary>Возможность: потоковая отправка тела из файлов (бинарная часть multipart из файла).</summary>
        bool SupportsFileBodySegments { get; }

        /// <summary>Возможность: промежуточный прогресс отправки.</summary>
        bool SupportsUploadProgress { get; }

        /// <summary>Возможность: промежуточный прогресс скачивания.</summary>
        bool SupportsDownloadProgress { get; }

        /// <summary>
        /// Заголовки, которые транспорт не позволяет надёжно установить на текущей платформе (значение будет
        /// перезаписано, проигнорировано или не поддержано). Сравнение без учёта регистра. Запрос с таким заголовком —
        /// fast-fail; автоматический User-Agent ставится, только если его нет в наборе.
        /// </summary>
        IReadOnlyCollection<string> UnsettableHeaders { get; }

        /// <summary>Возможность: лимит редиректов из настроек драйвера.</summary>
        bool SupportsRedirectLimit { get; }

        /// <summary>
        /// Создать объект попытки. Попытка не запускается до <see cref="INetworkAttempt.Start"/>.
        /// Если создание бросает исключение, драйвер сам освобождает всё, что успел выделить: объекта для
        /// <see cref="IDisposable.Dispose"/> у ядра в этом случае нет.
        /// </summary>
        INetworkAttempt CreateAttempt(PreparedAttempt attempt);
    }

    /// <summary>Одна попытка запроса, обёртка над нативным запросом транспорта. Управляется контроллером.</summary>
    public interface INetworkAttempt
    {
        void Start();

        /// <summary>Попытка завершена (успешно или с транспортной ошибкой).</summary>
        bool IsDone { get; }

        /// <summary>Прогресс отправки 0…1. Неизвестен — 0.</summary>
        float UploadProgress { get; }

        /// <summary>Прогресс скачивания 0…1. Неизвестен — 0.</summary>
        float DownloadProgress { get; }

        /// <summary>Сколько байт ответа получено (для лимита размера).</summary>
        long DownloadedBytes { get; }

        /// <summary>Результат. Только после <see cref="IsDone"/>.</summary>
        AttemptResult GetResult();

        /// <summary>Прервать (таймаут, отмена, превышен размер).</summary>
        void Abort();

        /// <summary>Освободить нативные ресурсы. Вызывается контроллером на любом исходе.</summary>
        void Dispose();
    }

    /// <summary>Подготовленная попытка: всё, что нужно транспорту, уже собрано контроллером.</summary>
    public sealed class PreparedAttempt
    {
        /// <summary>Идентификатор запроса (<see cref="Query.Id"/>).</summary>
        public long QueryId { get; }

        /// <summary>Номер попытки в рамках запроса, с 1 (включая повтор после обновления токена).</summary>
        public int AttemptNumber { get; }

        public string Url { get; }
        public HttpMethod Method { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Content-Type тела. <c>null</c>, если тела нет.</summary>
        public string ContentType { get; }

        /// <summary>Тело как последовательность сегментов (байты и файлы). Пусто — тела нет.</summary>
        public IReadOnlyList<BodySegment> Body { get; }

        /// <summary>Путь для скачивания ответа в файл (временный, переименовывает контроллер). <c>null</c> — в память.</summary>
        public string DownloadPath { get; }

        public PreparedAttempt(long queryId, int attemptNumber, string url, HttpMethod method,
            IReadOnlyDictionary<string, string> headers, string contentType, IReadOnlyList<BodySegment> body,
            string downloadPath)
        {
            QueryId = queryId;
            AttemptNumber = attemptNumber;
            Url = url;
            Method = method;
            Headers = headers;
            ContentType = contentType;
            Body = body;
            DownloadPath = downloadPath;
        }
    }

    /// <summary>Сегмент тела: байты в памяти или файл с диска.</summary>
    public readonly struct BodySegment
    {
        public byte[] Bytes { get; }
        public string FilePath { get; }

        private BodySegment(byte[] bytes, string filePath)
        {
            Bytes = bytes;
            FilePath = filePath;
        }

        public static BodySegment FromBytes(byte[] bytes) => new(bytes, null);
        public static BodySegment FromFile(string path) => new(null, path);
    }

    /// <summary>Транспортная категория неудачи попытки.</summary>
    public enum TransportError
    {
        None,

        /// <summary>Обрыв, недоступен хост, превышен лимит редиректов и т.п.</summary>
        Network
    }

    /// <summary>Результат попытки от драйвера.</summary>
    public sealed class AttemptResult
    {
        public long StatusCode { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Тело в памяти. <c>null</c> при скачивании в файл.</summary>
        public byte[] Body { get; }

        public TransportError Error { get; }
        public string ErrorMessage { get; }

        public AttemptResult(long statusCode, IReadOnlyDictionary<string, string> headers, byte[] body,
            TransportError error, string errorMessage)
        {
            StatusCode = statusCode;
            Headers = headers;
            Body = body;
            Error = error;
            ErrorMessage = errorMessage;
        }
    }
}
