namespace Vortex.Core.StateGraphSystem.Model
{
    /// <summary>
    /// Реакция узла на <c>Next</c>, когда ни один переход «по Next» не прошёл по условиям
    /// (или их в узле нет)
    /// </summary>
    public enum NextMissBehavior
    {
        /// <summary>Промах молча игнорируется</summary>
        Ignore,

        /// <summary>Промах — предупреждение в лог</summary>
        Warning,

        /// <summary>Промах — ошибка в лог</summary>
        Error
    }
}
