using System;

namespace Vortex.Core.NetworkSystem.Driver
{
    /// <summary>
    /// Одна попытка в транспорте: запустить, читать прогресс отправки и скачивания, узнать о готовности,
    /// получить результат, прервать, освободить ресурсы. Таймаут, отмену и наблюдение ведёт контроллер.
    /// </summary>
    public interface INetworkAttempt : IDisposable
    {
        /// <summary>Запустить попытку.</summary>
        void Run();

        /// <summary>Прогресс отправки, 0…1. Транспорт без прогресса отправки — 0 до завершения, затем 1.</summary>
        float UploadProgress { get; }

        /// <summary>Прогресс скачивания, 0…1. Размер ответа неизвестен — 0 до завершения, затем 1.</summary>
        float DownloadProgress { get; }

        /// <summary>Попытка завершена: результат готов.</summary>
        bool IsDone { get; }

        /// <summary>Результат попытки. До завершения — <c>null</c>.</summary>
        AttemptResult Result { get; }

        /// <summary>Прервать попытку.</summary>
        void Abort();

        // Dispose() — освободить ресурсы транспорта и временные файлы на любом исходе; повторный вызов безопасен.
    }
}
