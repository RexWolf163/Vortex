using System.Collections.Generic;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Итог сопоставления строки с маской: совпала ли строка целиком и захваченные куски «имя → значение».
    /// При несовпадении словарь захватов пуст.
    /// </summary>
    public sealed class MatchResult
    {
        /// <summary>Строка совпала с правилом целиком.</summary>
        public bool IsMatch { get; }

        /// <summary>Захваты «имя → значение», только чтение. При несовпадении пуст.</summary>
        public IReadOnlyDictionary<string, string> Captures { get; }

        private MatchResult(bool isMatch, Dictionary<string, string> captures)
        {
            IsMatch = isMatch;
            Captures = captures;
        }

        /// <summary>Совпадения нет: захваты пусты.</summary>
        public static MatchResult NoMatch() => new(false, new Dictionary<string, string>());

        /// <summary>Совпадение без захватов.</summary>
        public static MatchResult Matched() => new(true, new Dictionary<string, string>());

        /// <summary>
        /// Совпадение с захватами. Захваты копируются: последующие изменения переданной коллекции
        /// результат не затрагивают.
        /// </summary>
        public static MatchResult Matched(IReadOnlyDictionary<string, string> captures) =>
            new(true, new Dictionary<string, string>(captures));
    }
}
