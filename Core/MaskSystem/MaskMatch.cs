using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Результат выполнения маски: совпала ли строка и какие значения захвачены.
    /// «Не совпало» и «совпало без захватов» различаются признаком <see cref="IsMatch"/>.
    /// </summary>
    public sealed class MaskMatch
    {
        private static readonly IReadOnlyDictionary<string, string> NoCaptures =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

        /// <summary>Строка не совпала с маской. Словарь захватов пуст.</summary>
        public static readonly MaskMatch NoMatch = new(false, NoCaptures);

        /// <summary>Строка совпала, захватов нет.</summary>
        public static readonly MaskMatch MatchWithoutCaptures = new(true, NoCaptures);

        /// <summary>Строка совпала с маской.</summary>
        public bool IsMatch { get; }

        /// <summary>Захваченные значения «имя → значение». При несовпадении всегда пуст.</summary>
        public IReadOnlyDictionary<string, string> Captures { get; }

        private MaskMatch(bool isMatch, IReadOnlyDictionary<string, string> captures)
        {
            IsMatch = isMatch;
            Captures = captures;
        }

        /// <summary>Совпадение с захватами. Словарь оборачивается в read-only и дальше не меняется.</summary>
        internal static MaskMatch Matched(Dictionary<string, string> captures) =>
            captures.Count == 0 ? MatchWithoutCaptures : new MaskMatch(true, new ReadOnlyDictionary<string, string>(captures));
    }
}
