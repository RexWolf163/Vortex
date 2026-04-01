using System;
using Sirenix.OdinInspector;

namespace Vortex.Unity.AppSystem.System.TimeSystem
{
    /// <summary>
    /// Структура экшена в очереди
    /// </summary>
    internal struct QueuedAction
    {
        /// <summary>
        /// Заказчик события
        /// </summary>
        [FoldoutGroup("$Timer")] public object Owner;

        /// <summary>
        /// Отложенный экшен 
        /// </summary>
        [FoldoutGroup("$Timer")] public Action Action;

        /// <summary>
        /// Отметка времени, в которую должен быть вызван
        /// </summary>
        [FoldoutGroup("$Timer")] public double Timestamp;

#if UNITY_EDITOR
        private string Timer()
        {
            var span = new TimeSpan((long)((Timestamp - TimeController.Time) * TimeController.TicksPerSecond));
            return span.ToString(@"hh\:mm\:ss");
        }
#endif
    }
}