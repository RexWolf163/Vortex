using System;
using System.Text;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Правило «строка равна образцу»: проверка настройки образца, подготовка копии входа и образца (обрезка, NFC)
    /// и сравнение готовой строки с образцом выбранным способом.
    /// </summary>
    public static class StringEqualController
    {
        /// <summary>Проверить настройку образца.</summary>
        /// <param name="pattern">Образец.</param>
        /// <param name="trimMode">Режим обрезки копии входа.</param>
        /// <param name="compareMode">Способ сравнения.</param>
        /// <param name="normalize">Нормализация NFC.</param>
        public static ValidationResult Validate(string pattern, TrimMode trimMode, StringCompareMode compareMode,
            bool normalize)
        {
            if (!Enum.IsDefined(typeof(TrimMode), trimMode))
                return ValidationResult.Invalid($"Неизвестный режим обрезки: {(int)trimMode}.");
            if (!Enum.IsDefined(typeof(StringCompareMode), compareMode))
                return ValidationResult.Invalid($"Неизвестный способ сравнения: {(int)compareMode}.");
            if (pattern == null)
                return ValidationResult.Invalid("Образец не задан.");
            if (pattern.Length == 0)
                return ValidationResult.Valid();

            var trimsStart = trimMode == TrimMode.TrimStart || trimMode == TrimMode.FullTrim;
            if (trimsStart && char.IsWhiteSpace(pattern[0]))
                return ValidationResult.Invalid(
                    "Образец начинается с пробельного символа, а начало входа обрезается: совпадения не будет.", 0);

            var trimsEnd = trimMode == TrimMode.TrimEnd || trimMode == TrimMode.FullTrim;
            if (trimsEnd && char.IsWhiteSpace(pattern[^1]))
                return ValidationResult.Invalid(
                    "Образец оканчивается пробельным символом, а конец входа обрезается: совпадения не будет.",
                    pattern.Length - 1);

            if (normalize)
            {
                var invalid = FindInvalidUnicode(pattern);
                if (invalid >= 0)
                    return ValidationResult.Invalid(
                        "Образец содержит одиночный суррогат: нормализация невозможна.", invalid);
                try
                {
                    _ = pattern.Normalize(NormalizationForm.FormC);
                }
                catch (ArgumentException e)
                {
                    return ValidationResult.Invalid($"Образец нельзя нормализовать: {e.Message}");
                }
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Индекс первого одиночного суррогата (невалидный Unicode, нормализация невозможна); <c>-1</c> — нет.
        /// </summary>
        public static int FindInvalidUnicode(string value)
        {
            if (value == null)
                throw Fail(new ArgumentNullException(nameof(value), "Строка не задана (null)."));
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    i++;
                else if (char.IsSurrogate(value[i]))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Подготовить копию входа: обрезка пробельных символов по <paramref name="trimMode"/> и, при
        /// <paramref name="normalize"/>, нормализация NFC. Вход, который нельзя нормализовать (невалидный Unicode), —
        /// данные, а не ошибка.
        /// </summary>
        /// <returns><c>true</c> — копия готова; <c>false</c> — вход нельзя нормализовать («не совпало»).</returns>
        public static bool TryPrepareInput(string input, TrimMode trimMode, bool normalize, out string ready)
        {
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), "Вход не задан (null)."));
            var trimmed = trimMode switch
            {
                TrimMode.None => input,
                TrimMode.TrimStart => input.TrimStart(),
                TrimMode.TrimEnd => input.TrimEnd(),
                TrimMode.FullTrim => input.Trim(),
                _ => throw Fail(new ArgumentOutOfRangeException(nameof(trimMode), trimMode,
                    "Неизвестный режим обрезки."))
            };

            ready = null;
            if (!normalize || trimmed.Length == 0)
            {
                ready = trimmed;
                return true;
            }

            if (FindInvalidUnicode(trimmed) >= 0)
                return false;
            try
            {
                ready = trimmed.Normalize(NormalizationForm.FormC);
                return true;
            }
            catch (ArgumentException)
            {
                // Нормализация невозможна на этой платформе (например, U+FFFE) — вход это данные: не совпало
                return false;
            }
        }

        /// <summary>
        /// Подготовить проверенный образец: при <paramref name="normalize"/> — нормализация NFC, иначе как есть.
        /// Образец, не прошедший <see cref="Validate"/>, — ошибка программиста.
        /// </summary>
        public static string PreparePattern(string pattern, bool normalize)
        {
            if (pattern == null)
                throw Fail(new ArgumentNullException(nameof(pattern), "Образец не задан (null)."));
            if (!normalize)
                return pattern;
            try
            {
                return pattern.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException e)
            {
                throw Fail(new ArgumentException($"Образец не проверен или неверен: {e.Message}", nameof(pattern), e));
            }
        }

        /// <summary>Сравнить готовую строку с образцом.</summary>
        /// <param name="ready">Готовая (подготовленная) копия входа.</param>
        /// <param name="pattern">Проверенный образец.</param>
        /// <param name="compareMode">Способ сравнения.</param>
        /// <param name="ignoreCase">Не учитывать регистр.</param>
        public static bool IsMatch(string ready, string pattern, StringCompareMode compareMode, bool ignoreCase)
        {
            if (ready == null)
                throw Fail(new ArgumentNullException(nameof(ready), "Готовая строка не задана (null)."));
            if (pattern == null)
                throw Fail(new ArgumentNullException(nameof(pattern), "Образец не задан (null)."));
            return string.Equals(ready, pattern, ToComparison(compareMode, ignoreCase));
        }

        private static StringComparison ToComparison(StringCompareMode compareMode, bool ignoreCase) =>
            compareMode switch
            {
                StringCompareMode.Ordinal => ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal,
                StringCompareMode.InvariantCulture => ignoreCase
                    ? StringComparison.InvariantCultureIgnoreCase
                    : StringComparison.InvariantCulture,
                _ => throw Fail(new ArgumentOutOfRangeException(nameof(compareMode), compareMode,
                    "Неизвестный способ сравнения."))
            };

        private static Exception Fail(Exception exception)
        {
            Log.Print(LogLevel.Error, exception.Message, nameof(StringEqualController));
            return exception;
        }
    }
}
