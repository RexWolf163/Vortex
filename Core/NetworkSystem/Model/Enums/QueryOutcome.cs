namespace Vortex.Core.NetworkSystem.Model.Enums
{
    /// <summary>Чем закончился запрос.</summary>
    public enum QueryOutcome
    {
        /// <summary>Итога нет: запрос не завершён.</summary>
        None = 0,

        /// <summary>Успех: ответ 2xx.</summary>
        Success = 1,

        /// <summary>Не изменено: 304 на условный запрос, тела нет.</summary>
        NotModified = 2,

        /// <summary>Сеть: обрыв, недоступен хост, превышен лимит редиректов.</summary>
        Network = 3,

        /// <summary>Таймаут: попытка не уложилась в отведённое время.</summary>
        Timeout = 4,

        /// <summary>Ошибка клиента: ответ 4xx.</summary>
        ClientError = 5,

        /// <summary>Ошибка сервера: ответ 5xx.</summary>
        ServerError = 6,

        /// <summary>Превышен размер: ответ или тело запроса больше лимита.</summary>
        SizeExceeded = 7,

        /// <summary>Ошибка тела: файла бинарной части multipart нет на момент отправки.</summary>
        BodyError = 8,

        /// <summary>Отменён: внешним токеном, освобождением до завершения или выходом из приложения.</summary>
        Cancelled = 9,

        /// <summary>Сеть выключена глобальным выключателем.</summary>
        NetworkDisabled = 10,

        /// <summary>Редирект: сервер ответил переходом (3xx, кроме 304), переход запрещён и не выполнен.</summary>
        Redirect = 11
    }
}
