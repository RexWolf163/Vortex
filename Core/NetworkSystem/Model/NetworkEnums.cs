namespace Vortex.Core.NetworkSystem
{
    /// <summary>HTTP-метод запроса.</summary>
    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Patch,
        Delete,
        Head
    }

    /// <summary>Состояние запроса (жизненный цикл).</summary>
    public enum QueryState
    {
        /// <summary>Собирается цепочкой, ещё не запущен.</summary>
        Draft,

        /// <summary>Запущен, ждёт слот лимита одновременных запросов.</summary>
        Queued,

        /// <summary>Выполняется попытка.</summary>
        Running,

        /// <summary>Пауза перед повтором. Слот лимита свободен.</summary>
        RetryWait,

        /// <summary>Итог присвоен.</summary>
        Completed,

        /// <summary>Освобождён: исключён из реестра, данные остаются у владельца.</summary>
        Released
    }

    /// <summary>Итог запроса.</summary>
    public enum QueryOutcome
    {
        /// <summary>Итога ещё нет.</summary>
        None,

        /// <summary>2xx.</summary>
        Success,

        /// <summary>304 — ответ на условный запрос: тела нет, используйте свою копию.</summary>
        NotModified,

        /// <summary>Обрыв, недоступен хост, превышен лимит редиректов, иной транспортный сбой.</summary>
        Network,

        /// <summary>Попытка не уложилась в таймаут.</summary>
        Timeout,

        /// <summary>4xx.</summary>
        ClientError,

        /// <summary>5xx.</summary>
        ServerError,

        /// <summary>Ответ или тело запроса больше лимита.</summary>
        TooLarge,

        /// <summary>
        /// Ошибка файла тела: файла части multipart нет на момент отправки, или скачанный ответ не удалось
        /// записать на место (<see cref="Query.DownloadToFile"/>).
        /// </summary>
        BodyError,

        /// <summary>Отменён (внешний токен, освобождение до завершения, выход из приложения).</summary>
        Cancelled,

        /// <summary>Глобальный выключатель сети выключен.</summary>
        NetworkDisabled
    }
}
