namespace Vortex.Core.AudioSystem.Model
{
    /// <summary>
    /// Состояние воспроизведения обёртки звука.
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>Ожидает старта (создан, но ещё не заиграл — напр. ждёт фейд-ин музыки).</summary>
        Pending = 0,

        /// <summary>Играет.</summary>
        Playing = 1,

        /// <summary>На паузе.</summary>
        Paused = 2,

        /// <summary>Завершён (остановлен либо вытеснен) — управление больше недоступно.</summary>
        Finished = 3,
    }
}
