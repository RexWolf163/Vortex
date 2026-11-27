using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Правило «строка соответствует выражению»: проверка выражения компиляцией, сборка выражения с привязкой
    /// ко всей строке, сопоставление с извлечением захватов.
    /// </summary>
    /// <remarks>
    /// Таймаута сопоставления нет: выражения пишет разработчик, тяжёлые выражения на длинных строках —
    /// ответственность автора.
    /// </remarks>
    public static class RegexController
    {
        /// <summary>Проверить выражение: исходное и с привязкой ко всей строке должны компилироваться.</summary>
        public static ValidationResult Validate(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return ValidationResult.Invalid("Выражение не задано.");

            // Исходное — до обёртки: обёртка может «починить» выражение вида a)|(b
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException e)
            {
                return ValidationResult.Invalid(e.Message);
            }

            try
            {
                _ = Build(pattern, false);
            }
            catch (ArgumentException e)
            {
                return ValidationResult.Invalid(
                    $"Выражение нельзя привязать ко всей строке: {e.Message} Возможно, комментарий «#» в конце " +
                    "выражения (режим x) поглотил закрывающую скобку привязки — закройте комментарий переводом строки.");
            }

            return ValidationResult.Valid();
        }

        /// <summary>Собрать выражение с неявной привязкой ко всей строке.</summary>
        /// <param name="pattern">Проверенное выражение.</param>
        /// <param name="ignoreCase">Не учитывать регистр.</param>
        public static Regex Build(string pattern, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(pattern))
                throw Fail(new ArgumentException("Выражение не задано.", nameof(pattern)));
            var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return new Regex(@"\A(?:" + pattern + @")\z", options);
        }

        /// <summary>Подходит ли строка под выражение (без извлечения захватов).</summary>
        public static bool IsMatch(Regex regex, string input)
        {
            if (regex == null)
                throw Fail(new ArgumentNullException(nameof(regex), "Выражение не задано (null)."));
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), "Вход не задан (null)."));
            return regex.IsMatch(input);
        }

        /// <summary>
        /// Сопоставить строку с выражением. Захваты: именованные группы — по имени, неименованные — по номеру;
        /// группа 0 и не участвовавшие в совпадении группы не входят.
        /// </summary>
        public static MatchResult Match(Regex regex, string input)
        {
            if (regex == null)
                throw Fail(new ArgumentNullException(nameof(regex), "Выражение не задано (null)."));
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), "Вход не задан (null)."));

            var match = regex.Match(input);
            if (!match.Success)
                return MatchResult.NoMatch();

            var captures = new Dictionary<string, string>();
            foreach (var name in regex.GetGroupNames())
            {
                if (name == "0")
                    continue;
                var group = match.Groups[name];
                if (group.Success)
                    captures[name] = group.Value;
            }

            return MatchResult.Matched(captures);
        }

        private static Exception Fail(Exception exception)
        {
            Log.Print(LogLevel.Error, exception.Message, nameof(RegexController));
            return exception;
        }
    }
}
