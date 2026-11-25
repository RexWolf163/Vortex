using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Маска «регулярное выражение» (синтаксис .NET).
    /// <list type="bullet">
    /// <item>Привязка к границам строки неявная и охватывает всё выражение, включая альтернативы верхнего уровня:
    /// <c>a|b</c> совпадает только с <c>a</c> или <c>b</c>.</item>
    /// <item>Именованные группы <c>(?&lt;name&gt;…)</c> дают ключ по имени, неименованные — ключ-номер (<c>"1"</c>, <c>"2"</c>…).</item>
    /// <item>Группа 0 (совпадение целиком) и группы, не участвовавшие в совпадении, в результат не входят.</item>
    /// </list>
    /// Из кода: <c>new RegexMask { pattern = @"v(?&lt;ver&gt;\d+)/.*" }</c>, затем <see cref="Mask.Validate"/>.
    /// В инспекторе выбирается наследник слоя Unity.
    /// </summary>
    [Serializable]
    public class RegexMask : Mask
    {
        [NonSerialized] private Regex _regex;
        [NonSerialized] private int[] _groupNumbers;
        [NonSerialized] private string[] _groupKeys;

        protected override bool IsMatchCore(string input) => _regex.IsMatch(input);

        protected override MaskMatch MatchCore(string input)
        {
            var m = _regex.Match(input);
            if (!m.Success)
                return MaskMatch.NoMatch;

            var captures = new Dictionary<string, string>(_groupNumbers.Length, StringComparer.Ordinal);
            for (var i = 0; i < _groupNumbers.Length; i++)
            {
                var group = m.Groups[_groupNumbers[i]];
                if (group.Success)
                    captures[_groupKeys[i]] = group.Value;
            }

            return MaskMatch.Matched(captures);
        }

        protected override MaskValidation Build()
        {
            _regex = null;
            _groupNumbers = null;
            _groupKeys = null;

            var options = RegexOptions.CultureInvariant;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            // Сначала выражение как есть: обёртка может «починить» невалидное (a)|(b → \A(?:a)|(b)\z)
            // и тем самым сломать привязку к границам.
            try
            {
                _ = new Regex(pattern, options);
            }
            catch (ArgumentException e)
            {
                return MaskValidation.Invalid("Выражение не компилируется: " + e.Message);
            }

            try
            {
                _regex = new Regex("\\A(?:" + pattern + ")\\z", options);
            }
            catch (ArgumentException e)
            {
                return MaskValidation.Invalid(
                    "Выражение нельзя привязать к границам строки (например, комментарий # в режиме (?x) до конца шаблона): "
                    + e.Message);
            }

            var numbers = new List<int>();
            foreach (var number in _regex.GetGroupNumbers())
                if (number != 0)
                    numbers.Add(number);

            _groupNumbers = numbers.ToArray();
            _groupKeys = new string[_groupNumbers.Length];
            for (var i = 0; i < _groupNumbers.Length; i++)
                _groupKeys[i] = _regex.GroupNameFromNumber(_groupNumbers[i]);

            return MaskValidation.Valid;
        }
    }
}
