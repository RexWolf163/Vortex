using System;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Выбор типа маски выпадающим списком для поля <c>[SerializeReference]</c> типа <see cref="Vortex.Core.MaskSystem.Mask"/>
    /// (или его наследника). В списке — наследники типа поля, объявленные вне сборки ядра масок: реализации слоя Unity
    /// и проектные. Реализации ядра в инспекторе не предлагаются.
    ///
    /// Под списком — параметры маски, подсказка по правилам шаблона (наведение на поле шаблона), подсветка ошибки
    /// шаблона и поле «Тестовая строка» с результатом сопоставления.
    ///
    /// <code>[SerializeReference, MaskSelector] private Mask urlMask;</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MaskSelectorAttribute : Attribute
    {
    }
}
