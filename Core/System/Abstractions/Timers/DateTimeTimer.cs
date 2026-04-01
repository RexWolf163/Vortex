using System;

namespace Vortex.Core.System.Abstractions.Timers
{
    /// <summary>
    /// Класс таймера, для контроля промежутков времени.
    /// Для ситуаций когда известно точное календарное время
    /// начала и время завершения таймера. например: доступность бандла
    /// (в том числе в оффлайн режиме)
    /// </summary>
    public class DateTimeTimer
    {
        public DateTime Start { get; protected set; }

        public DateTime End { get; protected set; }

        /// <summary>
        /// Длительность таймера
        /// </summary>
        public TimeSpan Duration { get; protected set; }

        /// <summary>
        /// Точка остановки таймера по команде Pause
        /// </summary>
        private float freezePoint = 0;

        public DateTimeTimer(DateTime end)
        {
            Start = DateTime.UtcNow;
            End = end;
            Duration = End - Start;
        }

        public DateTimeTimer(TimeSpan duration)
        {
            Start = DateTime.UtcNow;
            End = DateTime.UtcNow.Add(duration);
            Duration = duration;
        }

        public DateTimeTimer(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
            Duration = End - Start;
        }

        /// <summary>
        /// Таймер завершен
        /// </summary>
        /// <returns></returns>
        public bool IsComplete() => End <= DateTime.UtcNow;

        /// <summary>
        /// Таймер запущен
        /// </summary>
        /// <returns></returns>
        public bool IsStarted() => Start <= DateTime.UtcNow;

        /// <summary>
        /// Сколько времени осталось
        /// </summary>
        /// <returns>Вернет общую длительность если точка начала еще не достигнута</returns>
        public TimeSpan GetTimeRemains()
        {
            if (IsComplete())
                return TimeSpan.Zero;
            return IsStarted() ? End - DateTime.UtcNow : Duration;
        }

        /// <summary>
        /// Сколько времени прошло.
        /// </summary>
        /// <returns>вернет 0 если точка начала еще не достигнута</returns>
        public TimeSpan GetTimeLeft()
        {
            if (IsComplete())
                return Duration;
            return IsStarted() ? DateTime.UtcNow - Start : TimeSpan.Zero;
        }

        public override string ToString() => $"DateTimeTimer from {Start} to {End} (duration: {Duration})";
    }
}