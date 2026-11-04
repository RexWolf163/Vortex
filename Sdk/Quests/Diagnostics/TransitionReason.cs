namespace Vortex.Sdk.Quests.Diagnostics
{
    /// <summary>
    /// Причина смены состояния квеста. Пишется в журнал монитора квестов (только редактор);
    /// в билде — аргумент <see cref="QuestModel.SetState"/> без эффекта.
    /// </summary>
    public enum TransitionReason
    {
        /// <summary>Unset → Locked на новой игре или загрузке</summary>
        Init,

        /// <summary>Условия старта выполнены: Locked → Ready</summary>
        ConditionsMet,

        /// <summary>Запуск логик: Ready → InProgress</summary>
        Started,

        /// <summary>Продолжение InProgress-квеста после загрузки (запись без смены состояния)</summary>
        Restored,

        /// <summary>Все логики пройдены: InProgress → Reward</summary>
        LogicsDone,

        /// <summary>Наград нет: Reward → Completed</summary>
        AutoCompleteNoRewards,

        /// <summary>Логика вернула false: → Failed / Locked (UnFailable)</summary>
        LogicFailed,

        /// <summary>Исключение в логике или выход из цикла в незавершённом состоянии: принудительный Completed</summary>
        LogicException,

        /// <summary>Награды выданы: Reward → Completed</summary>
        RewardsGiven,

        /// <summary>Исключение при выдаче наград: Reward → Completed</summary>
        RewardException,

        /// <summary>Сработали условия прерывания: → Blocked</summary>
        Interrupted,

        /// <summary>Условия прерывания снялись: Blocked → Locked</summary>
        Unblocked,

        /// <summary>Сброс в Unset при GameStates.Off / Loading</summary>
        GameStateReset,

        /// <summary>Действие из монитора квестов</summary>
        DebugAction
    }

    /// <summary>
    /// Набор условий квеста, проверку которого фиксирует монитор.
    /// </summary>
    public enum ConditionSetKind
    {
        Start,
        Interrupt
    }
}
