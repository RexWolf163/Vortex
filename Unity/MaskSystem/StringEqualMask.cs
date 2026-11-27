using System;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Маска вида «равенство строк»: настройка образца и сравнения, подготовка копии входа (обрезка, NFC)
    /// и подключение к <see cref="StringEqualController"/>.
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
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), $"[{GetType().Name}] Вход не задан (null)."));
            RequireValid();
            var ready = Prepare(input);
            // Вход, который нельзя нормализовать (невалидный Unicode), — данные, а не ошибка: не совпало
            if (ready == null)
                return false;
            _preparedPattern ??= normalize ? pattern.Normalize(NormalizationForm.FormC) : pattern;
            return StringEqualController.IsMatch(ready, _preparedPattern, compareMode, !caseSensitive);
        }

        /// <inheritdoc/>
        public override MatchResult Match(string input) =>
            IsMatch(input) ? MatchResult.Matched() : MatchResult.NoMatch();

        /// <summary>Копия входа после обрезки и нормализации; <c>null</c> — вход нельзя нормализовать.</summary>
        private string Prepare(string input)
        {
            var ready = trimMode switch
            {
                TrimMode.TrimStart => input.TrimStart(),
                TrimMode.TrimEnd => input.TrimEnd(),
                TrimMode.FullTrim => input.Trim(),
                _ => input
            };
            if (!normalize || ready.Length == 0)
                return ready;
            if (StringEqualController.FindInvalidUnicode(ready) >= 0)
                return null;
            try
            {
                return ready.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                // Нормализация невозможна на этой платформе (например, U+FFFE) — вход это данные: не совпало
                return null;
            }
        }

        private void RequireValid()
        {
            var validation = Validate();
            if (!validation.IsValid)
                throw Fail(new InvalidOperationException(
                    $"[{GetType().Name}] Сопоставление на неверной настройке: {validation.Message}"));
        }

        private static Exception Fail(Exception exception)
        {
            Debug.LogError(exception.Message);
            return exception;
        }

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
