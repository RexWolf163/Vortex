using System;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Маска — правило сопоставления строки. Три операции: проверка (<see cref="IsMatch"/>), выполнение с извлечением
    /// захватов (<see cref="Match"/>) и проверка собственного шаблона (<see cref="Validate"/>).
    /// Совпадение всегда со всей строкой. Синтаксис шаблона и захватов задаёт реализация.
    ///
    /// Жизненный цикл: маска создаётся (в инспекторе как <c>[SerializeReference]</c> у потребителя или кодом через
    /// <c>new</c>), потребитель валидирует её при своей инициализации, дальше маска только используется.
    /// После первой валидации параметры неизменны: изменение обнаруживается при следующем обращении и даёт исключение.
    /// Наследник может разрешить перевалидацию на лету (<see cref="RebuildOnChange"/>) — так делает слой Unity в редакторе.
    ///
    /// Использование на невалидной маске и <c>null</c> на входе — ошибки программиста (fail-fast).
    /// Только главный поток: потокобезопасность не обеспечивается.
    /// </summary>
    [Serializable]
    public abstract class Mask
    {
        /// <summary>Шаблон маски. Синтаксис — по реализации.</summary>
        public string pattern;

        /// <summary>Учитывать регистр при сравнении. Захваты возвращаются в регистре входной строки.</summary>
        public bool caseSensitive = true;

        [NonSerialized] private bool _built;
        [NonSerialized] private string _builtPattern;
        [NonSerialized] private bool _builtCaseSensitive;
        [NonSerialized] private MaskValidation _validation;

        /// <summary>
        /// Разрешено ли пересобрать маску, если её параметры изменились после валидации.
        /// <c>false</c> (по умолчанию) — изменение является ошибкой программиста.
        /// </summary>
        protected virtual bool RebuildOnChange => false;

        /// <summary>Проверить шаблон. Результат кэшируется до изменения параметров.</summary>
        public MaskValidation Validate()
        {
            EnsureBuilt();
            return _validation;
        }

        /// <summary>Совпадает ли строка с маской целиком.</summary>
        public bool IsMatch(string input)
        {
            RequireUsable(input);
            return IsMatchCore(input);
        }

        /// <summary>Сопоставить строку и извлечь захваты.</summary>
        public MaskMatch Match(string input)
        {
            RequireUsable(input);
            return MatchCore(input);
        }

        /// <summary>
        /// Собрать внутреннее представление по текущим параметрам и вернуть результат валидации.
        /// Пустой шаблон отсекается до вызова. При невалидном результате сопоставление не вызывается.
        /// </summary>
        protected abstract MaskValidation Build();

        /// <summary>Проверка на собранной валидной маске. Должна совпадать с <see cref="MatchCore"/>.<see cref="MaskMatch.IsMatch"/>.</summary>
        protected abstract bool IsMatchCore(string input);

        /// <summary>Выполнение на собранной валидной маске.</summary>
        protected abstract MaskMatch MatchCore(string input);

        /// <summary>Изменились ли параметры наследника с момента последней сборки.</summary>
        protected virtual bool ExtraParametersChanged() => false;

        /// <summary>Запомнить параметры наследника, с которыми собрана маска.</summary>
        protected virtual void RememberExtraParameters() { }

        private void RequireUsable(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            EnsureBuilt();
            if (!_validation.IsValid)
                throw new InvalidOperationException(
                    $"[MaskSystem] Mask '{pattern}' is invalid and cannot be used: {_validation.Message}");
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                var changed = !string.Equals(_builtPattern, pattern, StringComparison.Ordinal)
                              || _builtCaseSensitive != caseSensitive
                              || ExtraParametersChanged();
                if (!changed)
                    return;

                if (!RebuildOnChange)
                    throw new InvalidOperationException(
                        $"[MaskSystem] Mask '{_builtPattern}' was changed after validation. Masks are immutable once validated.");
            }

            _validation = string.IsNullOrEmpty(pattern) ? MaskValidation.Invalid("Шаблон пуст.", 0) : Build();
            _builtPattern = pattern;
            _builtCaseSensitive = caseSensitive;
            RememberExtraParameters();
            _built = true;
        }
    }
}
