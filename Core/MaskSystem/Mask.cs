using System;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Правило сопоставления строки целиком, заданное только операциями: проверить строку,
    /// выполнить сопоставление, проверить свою настройку.
    /// </summary>
    /// <remarks>
    /// Контракт:
    /// <list type="number">
    /// <item><c>IsMatch(input)</c> равно <c>Match(input).IsMatch</c> для любой строки.</item>
    /// <item>При несовпадении захваты пусты.</item>
    /// <item><c>input</c> = <c>null</c> — ошибка программиста.</item>
    /// <item>Вход не изменяется.</item>
    /// <item>До использования настройка проверяется через <see cref="Validate"/>; сопоставление на неверной
    /// настройке — ошибка программиста (fast-fail).</item>
    /// </list>
    /// </remarks>
    [Serializable]
    public abstract class Mask
    {
        /// <summary>Проверить строку. Равно <c>Match(input).IsMatch</c> для любой строки.</summary>
        /// <param name="input"><c>null</c> — ошибка программиста. Вход не изменяется.</param>
        public abstract bool IsMatch(string input);

        /// <summary>Выполнить сопоставление. При несовпадении захваты пусты.</summary>
        /// <param name="input"><c>null</c> — ошибка программиста. Вход не изменяется.</param>
        public abstract MatchResult Match(string input);

        /// <summary>Проверить свою настройку.</summary>
        public abstract ValidationResult Validate();
    }
}
