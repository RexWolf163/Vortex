using System;

namespace Vortex.Core.System.Abstractions
{
    /// <summary>
    /// Интерфейс классов, которые нуждаются в задержке отображения,
    /// например для подгрузки каких-то ресурсов
    /// </summary>
    public interface INeedDelay
    {
        /// <summary>
        /// Ожидание завершилось
        /// </summary>
        public event Action<INeedDelay> OnReady;

        /// <summary>
        /// Событие когда ожидание завершилось неудачно
        /// </summary>
        public event Action<INeedDelay> OnError;

        /// <summary>
        /// Проверка факта готовности (не зависимо от того с ошибкой или без)
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Проблемное завершение
        /// </summary>
        public bool IsFailed { get; }
    }
}