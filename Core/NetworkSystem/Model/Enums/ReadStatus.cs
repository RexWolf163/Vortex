namespace Vortex.Core.NetworkSystem.Model.Enums
{
    /// <summary>Исход чтения тела ответа в объект.</summary>
    public enum ReadStatus
    {
        /// <summary>Удалось: объект получен.</summary>
        Success = 0,

        /// <summary>Нет данных: тела нет (304, пустой ответ).</summary>
        NoData = 1,

        /// <summary>Не удалось: тело есть, но не разобрано.</summary>
        Failed = 2
    }
}
