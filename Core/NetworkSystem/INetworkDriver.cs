using System.Collections.Generic;
using Vortex.Core.NetworkSystem.Driver;
using Vortex.Core.NetworkSystem.Settings;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Прокладка к транспорту: отдаёт настройки связи, превращает подготовленную попытку в объект попытки,
    /// объявляет возможности транспорта на текущей платформе. Логики запросов не содержит.
    /// </summary>
    public interface INetworkDriver : ISystemDriver
    {
        /// <summary>Настройки связи драйвера.</summary>
        INetworkSettings GetSettings();

        /// <summary>Создать объект попытки по подготовленной попытке. Попытка не запускается.</summary>
        INetworkAttempt CreateAttempt(PreparedAttempt attempt);

        /// <summary>Возможность: скачивание ответа в файл.</summary>
        bool CanDownloadToFile { get; }

        /// <summary>Возможность: потоковая отправка сегмента тела из файла.</summary>
        bool CanStreamBodyFromFile { get; }

        /// <summary>Возможность: прогресс отправки.</summary>
        bool HasUploadProgress { get; }

        /// <summary>Возможность: прогресс скачивания.</summary>
        bool HasDownloadProgress { get; }

        /// <summary>
        /// Заголовки, которые транспорт на текущей платформе не даёт установить. Запрос с таким заголовком —
        /// ошибка программиста.
        /// </summary>
        IReadOnlyCollection<string> ForbiddenHeaders { get; }

        /// <summary>Возможность: лимит числа редиректов.</summary>
        bool CanLimitRedirects { get; }

        /// <summary>
        /// Возможность: запрет переходов для отдельной попытки. Без неё запрет не применяется —
        /// переходы выполняет транспорт.
        /// </summary>
        bool CanForbidRedirects { get; }

        /// <summary>Сеть сейчас доступна. Только подсказка для логов: запросы не блокирует.</summary>
        bool IsNetworkReachable { get; }
    }
}
