namespace Vortex.Core.NetworkSystem.Driver
{
    /// <summary>Почему попытка не дала HTTP-ответа.</summary>
    public enum AttemptError
    {
        /// <summary>Ошибки нет: HTTP-ответ получен.</summary>
        None = 0,

        /// <summary>Сеть: обрыв, недоступен хост, сбой транспорта.</summary>
        Network = 1,

        /// <summary>Нет файла тела: файл сегмента тела отсутствует на момент отправки.</summary>
        BodyFileMissing = 2,

        /// <summary>Превышен размер ответа: ответ больше лимита, попытка прервана.</summary>
        ResponseSizeExceeded = 3
    }
}
