using System;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.TextInputSystem.Filters
{
    /// <summary>
    /// Маска регулярными выражениями: одно — для строки во время набора, второе — для итога.
    /// Выражение проверяет строку целиком, якоря <c>^…$</c> ставит автор. Пустое выражение пропускает любую строку
    /// </summary>
    [Serializable]
    public class RegexTextFilter : ITextInputFilter
    {
        [SerializeField, LabelText("Во время набора")]
        [Tooltip("Должно принимать и недописанные строки. Пример для целого со знаком: ^-?\\d{0,3}$ " +
                 "(пусто и «-» — допустимы).")]
        private string partialPattern = "";

        [SerializeField, LabelText("Итог")]
        [Tooltip("Проверка при сохранении. Пример для целого со знаком: ^-?\\d{1,3}$.")]
        private string finalPattern = "";

        public bool IsValidPartial(string candidate) => IsMatch(candidate, partialPattern);

        public bool IsValidFinal(string text) => IsMatch(text, finalPattern);

        // Статический Regex.IsMatch кэширует разобранные выражения сам — своего кэша не держим, правка выражения
        // в инспекторе во время игры подхватывается сразу
        private static bool IsMatch(string text, string pattern) =>
            Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }
}
