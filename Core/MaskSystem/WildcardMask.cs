using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Маска «шаблон со <c>*</c>» — конвенция маршрутизаторов и glob:
    /// <list type="bullet">
    /// <item><c>*</c> — ноль или более символов внутри сегмента (не пересекает разделитель); в результат не входит.</item>
    /// <item><c>**</c> — ноль или более любых символов, включая разделитель; в результат не входит.</item>
    /// <item><c>{name}</c> — захват: один или более символов внутри сегмента.</item>
    /// <item><c>{*name}</c> — захват хвоста: ноль или более любых символов; только последним элементом шаблона.</item>
    /// <item><c>{{</c>, <c>}}</c>, <c>\*</c>, <c>\\</c> — литералы <c>{</c>, <c>}</c>, <c>*</c>, <c>\</c>.</item>
    /// </list>
    /// Два захвата подряд без литерала между ними (<c>{a}{b}</c>) запрещены: граница неоднозначна.
    /// При неоднозначности внутри сегмента (<c>{a}.{b}</c> на <c>x.y.z</c>) левый захват жадный: <c>a = x.y</c>.
    ///
    /// Из кода: <c>new WildcardMask { pattern = "/items/{id}" }</c>, затем <see cref="Mask.Validate"/>.
    /// В инспекторе выбирается наследник слоя Unity.
    /// </summary>
    [Serializable]
    public class WildcardMask : Mask
    {
        /// <summary>Разделитель сегментов. Не может быть служебным символом шаблона.</summary>
        public char separator = '/';

        [NonSerialized] private char _builtSeparator;
        [NonSerialized] private Regex _regex;
        [NonSerialized] private string[] _captureNames;
        [NonSerialized] private int[] _groupNumbers;

        protected override bool ExtraParametersChanged() => _builtSeparator != separator;

        protected override void RememberExtraParameters() => _builtSeparator = separator;

        protected override bool IsMatchCore(string input) => _regex.IsMatch(input);

        protected override MaskMatch MatchCore(string input)
        {
            var m = _regex.Match(input);
            if (!m.Success)
                return MaskMatch.NoMatch;

            var captures = new Dictionary<string, string>(_captureNames.Length, StringComparer.Ordinal);
            for (var i = 0; i < _captureNames.Length; i++)
                captures[_captureNames[i]] = m.Groups[_groupNumbers[i]].Value;

            return MaskMatch.Matched(captures);
        }

        protected override MaskValidation Build()
        {
            _regex = null;
            _captureNames = null;
            _groupNumbers = null;

            if (separator == '\0')
                return MaskValidation.Invalid("Разделитель сегментов не задан.");
            if (IsReserved(separator))
                return MaskValidation.Invalid($"Разделитель «{separator}» — служебный символ шаблона.");

            var p = pattern;
            var notSeparator = "[^" + EscapeForClass(separator) + "]";
            var regex = new StringBuilder("\\A");
            var names = new List<string>();
            var previousIsCapture = false;
            var tailCapturePosition = -1;

            var i = 0;
            while (i < p.Length)
            {
                if (tailCapturePosition >= 0)
                    return MaskValidation.Invalid("Захват хвоста {*имя} допустим только последним элементом шаблона.",
                        tailCapturePosition);

                var c = p[i];
                var next = i + 1 < p.Length ? p[i + 1] : '\0';

                switch (c)
                {
                    case '\\':
                        if (next != '*' && next != '\\')
                            return MaskValidation.Invalid("Неизвестное экранирование: допустимы \\* и \\\\.", i);
                        AppendLiteral(regex, next);
                        previousIsCapture = false;
                        i += 2;
                        continue;

                    case '}':
                        if (next != '}')
                            return MaskValidation.Invalid("Лишняя закрывающая скобка: литерал «}» записывается как «}}».", i);
                        AppendLiteral(regex, '}');
                        previousIsCapture = false;
                        i += 2;
                        continue;

                    case '*':
                        if (next == '*')
                        {
                            regex.Append(".*");
                            i += 2;
                        }
                        else
                        {
                            regex.Append(notSeparator).Append('*');
                            i++;
                        }

                        previousIsCapture = false;
                        continue;

                    case '{':
                        if (next == '{')
                        {
                            AppendLiteral(regex, '{');
                            previousIsCapture = false;
                            i += 2;
                            continue;
                        }

                        var close = p.IndexOf('}', i + 1);
                        if (close < 0)
                            return MaskValidation.Invalid("Незакрытая фигурная скобка.", i);

                        var body = p.Substring(i + 1, close - i - 1);
                        var isTail = body.StartsWith("*", StringComparison.Ordinal);
                        var name = isTail ? body.Substring(1) : body;

                        if (name.Length == 0)
                            return MaskValidation.Invalid("Захват без имени.", i);

                        var badIndex = IndexOfForbiddenNameChar(name);
                        if (badIndex >= 0)
                            return MaskValidation.Invalid($"Недопустимый символ «{name[badIndex]}» в имени захвата.",
                                i + 1 + (isTail ? 1 : 0) + badIndex);

                        if (previousIsCapture)
                            return MaskValidation.Invalid(
                                "Два захвата подряд без литерала между ними: граница неоднозначна.", i);

                        if (names.Contains(name))
                            return MaskValidation.Invalid($"Повторяющееся имя захвата «{name}».", i);

                        regex.Append("(?<g").Append(names.Count).Append('>')
                            .Append(isTail ? ".*" : notSeparator + "+")
                            .Append(')');
                        names.Add(name);

                        if (isTail)
                            tailCapturePosition = i;

                        previousIsCapture = true;
                        i = close + 1;
                        continue;

                    default:
                        AppendLiteral(regex, c);
                        previousIsCapture = false;
                        i++;
                        continue;
                }
            }

            regex.Append("\\z");

            var options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            _regex = new Regex(regex.ToString(), options);
            _captureNames = names.ToArray();
            _groupNumbers = new int[names.Count];
            for (var n = 0; n < names.Count; n++)
                _groupNumbers[n] = _regex.GroupNumberFromName("g" + n);

            return MaskValidation.Valid;
        }

        private static bool IsReserved(char c) => c == '*' || c == '{' || c == '}' || c == '\\';

        private int IndexOfForbiddenNameChar(string name)
        {
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (IsReserved(c) || c == separator)
                    return i;
            }

            return -1;
        }

        private static void AppendLiteral(StringBuilder regex, char c) => regex.Append(Regex.Escape(c.ToString()));

        private static string EscapeForClass(char c) =>
            c is ']' or '[' or '^' or '-' ? "\\" + c : Regex.Escape(c.ToString());
    }
}
