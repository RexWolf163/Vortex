using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Редакторная проверка маски в инспекторе: тестовая строка, итог проверки настройки с позицией ошибки,
    /// итог сопоставления тестовой строки (совпало / нет, захваты) и сверка инварианта 1 (<c>IsMatch</c> ↔ <c>Match</c>).
    /// </summary>
    [HideReferenceObjectPicker]
    public abstract partial class UnityMask
    {
        // Поле вне #if: раскладка сериализации в редакторе и в сборке должна совпадать.
        [SerializeField, PropertyOrder(100), LabelText("Тестовая строка")]
        [InfoBox("$ValidationMessage", InfoMessageType.Error, "$HasValidationError")]
        private string testString;

        /// <summary>
        /// Сопоставление тестовой строки — только по кнопке «Проверить», а не при вводе.
        /// По умолчанию — при вводе.
        /// </summary>
        protected virtual bool TestOnDemand => false;

        /// <summary>
        /// Сбросить итоги редакторной проверки. Вызывается видом при правке своих полей настройки.
        /// </summary>
        protected void ResetEditorCache()
        {
#if UNITY_EDITOR
            _validated = false;
            _validation = null;
            ResetTest();
#endif
        }

#if UNITY_EDITOR
        // Итоги считаются один раз на изменение (тестовой строки или настройки), а не на каждую перерисовку.
        // Сбой фиксируется так же: до следующего изменения повторного вызова нет.
        [NonSerialized] private bool _validated;
        [NonSerialized] private ValidationResult _validation;
        [NonSerialized] private bool _tested;
        [NonSerialized] private string _testedInput;
        [NonSerialized] private MatchResult _testResult;
        [NonSerialized] private Dictionary<string, string> _testCaptures;

        private void ResetTest()
        {
            _tested = false;
            _testResult = null;
            _testCaptures = null;
        }

        private void EnsureValidation()
        {
            if (_validated)
                return;
            // Отметка до вызова: при исключении в Validate проверка не повторяется до следующего изменения
            _validated = true;
            _validation = Validate();
        }

        private void EnsureTest(bool byButton)
        {
            // Пустое поле — пустая строка: это обычный вход
            var input = testString ?? string.Empty;
            if (input != _testedInput)
            {
                _testedInput = input;
                ResetTest();
            }

            if (_tested || (TestOnDemand && !byButton))
                return;

            EnsureValidation();
            if (_validation == null || !_validation.IsValid)
                return;

            // Отметка до вызова: при исключении в Match/IsMatch сопоставление не повторяется до следующего изменения
            _tested = true;
            var result = Match(input);
            var captures = new Dictionary<string, string>();
            foreach (var capture in result.Captures)
                captures.Add(capture.Key, capture.Value);

            var isMatch = IsMatch(input);
            if (isMatch != result.IsMatch)
                Debug.LogError($"[{GetType().Name}] IsMatch и Match расходятся на «{input}»: " +
                               $"IsMatch = {isMatch}, Match.IsMatch = {result.IsMatch}");

            _testResult = result;
            _testCaptures = captures;
        }

        private bool HasValidationError
        {
            get
            {
                EnsureValidation();
                return _validation != null && !_validation.IsValid;
            }
        }

        private string ValidationMessage
        {
            get
            {
                EnsureValidation();
                if (_validation == null)
                    return null;
                return _validation.Position < 0
                    ? _validation.Message
                    : $"{_validation.Message} (позиция {_validation.Position})";
            }
        }

        [Button("Проверить"), PropertyOrder(101), ShowIf("TestOnDemand")]
        private void RunTest() => EnsureTest(true);

        [ShowInInspector, PropertyOrder(102), LabelText("Результат"), DisplayAsString]
        private string TestOutcome
        {
            get
            {
                EnsureTest(false);
                if (_testResult == null)
                    return string.Empty;
                return _testResult.IsMatch ? "совпало" : "нет";
            }
        }

        private bool HasCaptures
        {
            get
            {
                EnsureTest(false);
                return _testCaptures != null && _testCaptures.Count > 0;
            }
        }

        [ShowInInspector, PropertyOrder(103), LabelText("Захваты"), ShowIf("HasCaptures")]
        [DictionaryDrawerSettings(IsReadOnly = true)]
        private Dictionary<string, string> TestCaptures
        {
            get
            {
                EnsureTest(false);
                return _testCaptures;
            }
        }
#endif
    }
}
