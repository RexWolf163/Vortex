namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>Происхождение значения слота.</summary>
    public enum SlotOrigin
    {
        /// <summary>Совпадает с заводским.</summary>
        Default,

        /// <summary>Заводская клавиша заменена.</summary>
        Overridden,

        /// <summary>Клавиша в слоте, которого в заводской раскладке не было.</summary>
        Added,

        /// <summary>Заводская клавиша снята.</summary>
        Cleared,

        /// <summary>Свободный слот, заводского не было.</summary>
        Empty
    }

    /// <summary>
    /// Уровень конфликта слота. Значения — номера состояний свитчера (<c>UIStateSwitcher.Set(Enum)</c>), поэтому
    /// новые члены добавляются только в конец. Тяжесть не следует из порядка членов: по возрастанию —
    /// <see cref="None"/>, <see cref="CrossMap"/>, <see cref="Common"/>, <see cref="IntraMapAllowed"/>,
    /// <see cref="IntraMap"/>.
    /// </summary>
    public enum SlotConflict
    {
        None,

        /// <summary>Та же клавиша в другой карте той же группы — допустимо, подсвечивается.</summary>
        CrossMap,

        /// <summary>Та же клавиша у другой команды той же карты, пара в исключениях конфига.</summary>
        IntraMapAllowed,

        /// <summary>Та же клавиша у другой команды той же карты — недопустимо.</summary>
        IntraMap,

        /// <summary>
        /// Та же клавиша в другой карте той же группы, и одна из сторон — сквозная команда (<c>commonCommands</c>
        /// конфига): действует поверх любой карты. Допустимо, подсвечивается отдельно от <see cref="CrossMap"/>.
        /// </summary>
        Common
    }
}
