using System;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Маска вида «регулярное выражение»: настройка выражения и регистра, кэш скомпилированного выражения
    /// и подключение к <see cref="RegexController"/>; проверка тестовой строки по кнопке.
    /// </summary>
    [Serializable]
    public class RegexMask : UnityMask
    {
        [SerializeField, LabelText("Выражение"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private string pattern = "";

        [SerializeField, LabelText("Учитывать регистр"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private bool caseSensitive = true;

        [NonSerialized] private ValidationResult _validation;
        [NonSerialized] private Regex _regex;

        /// <summary>Выражение.</summary>
        public string Pattern => pattern;

        /// <summary>Учитывать регистр.</summary>
        public bool CaseSensitive => caseSensitive;

        /// <inheritdoc/>
        public override string RulesHint =>
            "Регулярное выражение .NET. Совпадение — со всей строкой: привязка неявная и охватывает всё выражение, " +
            "включая альтернативы (a|b не совпадает с xa).\n" +
            "Захваты: именованные группы — по имени, неименованные — по номеру (1, 2, …); " +
            "группа 0 и не участвовавшие группы не попадают.\n" +
            "Регистр — настройка; сравнение без учёта культуры устройства.\n" +
            "Таймаута нет: тяжёлое выражение на длинной строке — ответственность автора.\n" +
            "Комментарий «#» в режиме x закрывайте переводом строки, иначе он поглотит привязку.\n" +
            "Пример: api\\.partner\\.com/(?<ver>v\\d+)/.* на api.partner.com/v2/x — ver = v2.";

        /// <inheritdoc/>
        protected override bool TestOnDemand => true;

        /// <inheritdoc/>
        public override ValidationResult Validate() => _validation ??= RegexController.Validate(pattern);

        /// <inheritdoc/>
        public override bool IsMatch(string input) => RegexController.IsMatch(RequireRegex(input), input);

        /// <inheritdoc/>
        public override MatchResult Match(string input) => RegexController.Match(RequireRegex(input), input);

        /// <summary>Проверенное и скомпилированное выражение; <c>null</c> на входе и неверная настройка — fast-fail.</summary>
        private Regex RequireRegex(string input)
        {
            RequireValid(input);
            return _regex ??= RegexController.Build(pattern, !caseSensitive);
        }

#if UNITY_EDITOR
        private void OnSettingsChanged()
        {
            _validation = null;
            _regex = null;
            ResetEditorCache();
        }
#endif
    }
}
