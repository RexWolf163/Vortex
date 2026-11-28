using System.Collections.Generic;
using Vortex.Core.NetworkSystem.Config;
using Vortex.Core.NetworkSystem.Driver;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Прокладка к транспорту: отдаёт настройки связи, превращает подготовленную попытку в объект попытки,
    /// объявляет возможности транспорта на текущей платформе и сообщает текущую доступность сети.
    /// Логики запросов не содержит.
    /// </summary>
    /// <remarks>
    /// Признаки возможностей и набор неустанавливаемых заголовков постоянны после инициализации драйвера.
    /// </remarks>
    public interface INetworkDriver : ISystemDriver
    {
        /// <summary>
        /// Настройки связи драйвера. Контроллер вызывает после сигнала готовности драйвера
        /// (<see cref="ISystemDriver.OnInit"/>). Объект один на всё время работы. <c>null</c> — ошибка
        /// конфигурации: fast-fail контроллера.
        /// </summary>
        INetworkSettings GetSettings();

        /// <summary>
        /// Создать объект попытки по подготовленной попытке. Попытка не запускается. <c>null</c> не возвращается.
        /// Исключение — только ошибка программиста: сбой транспорта и отсутствующий файл тела — результат
        /// попытки с категорией ошибки (<see cref="AttemptResult.Failed"/>).
        /// </summary>
        INetworkAttempt CreateAttempt(PreparedAttempt attempt);

        /// <summary>Возможность: скачивание ответа в файл.</summary>
        bool CanDownloadToFile { get; }

        /// <summary>Возможность: потоковая отправка сегмента тела из файла.</summary>
        bool CanStreamBodyFromFile { get; }

        /// <summary>
        /// Транспорт сообщает настоящий прогресс отправки. Справочный признак: контроллер по нему в запросе
        /// не отказывает. Без него прогресс отправки — 0 до завершения, затем 1.
        /// </summary>
        bool HasUploadProgress { get; }

        /// <summary>
        /// Транспорт сообщает настоящий прогресс скачивания. Справочный признак: контроллер по нему в запросе
        /// не отказывает. Без него прогресс скачивания — 0 до завершения, затем 1.
        /// </summary>
        bool HasDownloadProgress { get; }

        /// <summary>
        /// Заголовки, которые транспорт на текущей платформе не даёт установить. Запрос с таким заголовком —
        /// ошибка программиста. Имена сравниваются без учёта регистра. Ограничений нет — пустой набор,
        /// не <c>null</c>.
        /// </summary>
        IReadOnlyCollection<string> ForbiddenHeaders { get; }

        /// <summary>
        /// Возможность: запрет переходов для отдельной попытки. Без неё запрет не применяется —
        /// переходы выполняет транспорт.
        /// </summary>
        bool CanForbidRedirects { get; }

        /// <summary>
        /// Сеть сейчас доступна. Текущее состояние, а не возможность: меняется во время работы.
        /// Только подсказка для логов: запросы не блокирует.
        /// </summary>
        bool IsNetworkReachable { get; }
    }
}
