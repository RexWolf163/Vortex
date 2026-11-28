namespace Vortex.Core.NetworkSystem.Model.Enums
{
    /// <summary>Этап жизни запроса.</summary>
    public enum QueryState
    {
        /// <summary>Собирается: параметры ещё можно задавать.</summary>
        Building = 0,

        /// <summary>В очереди: запущен, ждёт свободный слот.</summary>
        Queued = 1,

        /// <summary>Выполняется: идёт попытка.</summary>
        Running = 2,

        /// <summary>Ожидание повтора: пауза между попытками, слот свободен.</summary>
        WaitingRetry = 3,

        /// <summary>Завершён: итог присвоен.</summary>
        Completed = 4,

        /// <summary>Освобождён: покинул реестр, данные остаются у владельца.</summary>
        Released = 5
    }
}
