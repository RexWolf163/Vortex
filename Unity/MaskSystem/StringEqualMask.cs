using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Маска вида «равенство строк»: настройка образца и сравнения и подключение к
    /// <see cref="StringEqualController"/> (подготовка копии входа и образца, сравнение).
    /// </summary>
    [Serializable]
    public class StringEqualMask : UnityMask
    {
        [SerializeField, LabelText("Образец"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private string pattern = "";

        [SerializeField, LabelText("Способ сравнения"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private StringCompareMode compareMode = StringCompareMode.Ordinal;

        [SerializeField, LabelText("Учитывать регистр"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private bool caseSensitive = true;

        [SerializeField, LabelText("Обрезка входа"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private TrimMode trimMode = TrimMode.None;

        [SerializeField, LabelText("Нормализация NFC"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private bool normalize;

        [NonSerialized] private ValidationResult _validation;
        [NonSerialized] private string _preparedPattern;

        /// <summary>Образец.</summary>
        public string Pattern => pattern;

        /// <summary>Способ сравнения.</summary>
        public StringCompareMode CompareMode => compareMode;

        /// <summary>Учитывать регистр.</summary>
        public bool CaseSensitive => caseSensitive;

        /// <summary>Режим обрезки копии входа.</summary>
        public TrimMode TrimMode => trimMode;

        /// <summary>Нормализация NFC копии входа и образца.</summary>
        public bool Normalize => normalize;

        /// <inheritdoc/>
        public override string RulesHint =>
            "Строка совпадает, если её копия после обрезки и нормализации (по настройке) целиком равна образцу.\n" +
            "Ordinal — точное совпадение символов; InvariantCulture — по правилам инвариантной культуры.\n" +
            "Пустой образец совпадает только с пустой строкой.\n" +
            "При InvariantCulture игнорируемые символы (нулевой ширины, мягкий перенос U+00AD, \\0 и т.п.) " +
            "не учитываются в любом образце, а канонически равные записи (é и e + ◌́) совпадают и без нормализации: " +
            "сравнение нестрогое.\n" +
            "Образец не обрезается: пробел на обрезаемом краю образца — ошибка настройки.\n" +
            "Пример: образец «Main», обрезка FullTrim — подходит «  Main ».";

        /// <inheritdoc/>
        public override ValidationResult Validate() =>
            _validation ??= StringEqualController.Validate(pattern, trimMode, compareMode, normalize);

        /// <inheritdoc/>
        public override bool IsMatch(string input)
        {
            RequireValid(input);
            // Вход, который нельзя нормализовать (невалидный Unicode), — данные, а не ошибка: не совпало
            if (!StringEqualController.TryPrepareInput(input, trimMode, normalize, out var ready))
                return false;
            _preparedPattern ??= StringEqualController.PreparePattern(pattern, normalize);
            return StringEqualController.IsMatch(ready, _preparedPattern, compareMode, !caseSensitive);
        }

        /// <inheritdoc/>
        public override MatchResult Match(string input) =>
            IsMatch(input) ? MatchResult.Matched() : MatchResult.NoMatch();

#if UNITY_EDITOR
        private void OnSettingsChanged()
        {
            _validation = null;
            _preparedPattern = null;
            ResetEditorCache();
        }
#endif
    }
}
