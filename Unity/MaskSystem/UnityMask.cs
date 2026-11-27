using System;
using UnityEngine;
using Vortex.Core.MaskSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Прокладка маски Unity-слоя: название вида для заголовка в инспекторе, текст правил вида для подсказки
    /// и общая проверка перед сопоставлением.
    /// </summary>
    [Serializable, ClassLabel("$GetLabel")]
    public abstract partial class UnityMask : Mask
    {
        /// <summary>Название вида для заголовка в инспекторе. По умолчанию — имя типа.</summary>
        protected virtual string GetLabel() => GetType().Name;

        /// <summary>Текст правил вида для подсказки.</summary>
        public abstract string RulesHint { get; }

        /// <summary>
        /// Проверка перед сопоставлением: вход не задан (<c>null</c>) или настройка неверна — ошибка программиста
        /// или настройки, fast-fail (лог ошибки и исключение).
        /// </summary>
        protected void RequireValid(string input)
        {
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), $"[{GetType().Name}] Вход не задан (null)."));
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
    }
}
