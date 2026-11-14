namespace Vortex.Sdk.AudioAdvancedControl.Model
{
    /// <summary>
    /// Режим проигрывания списка клипов
    /// </summary>
    public enum AdvancedPlaybackMode
    {
        /// <summary>
        /// Выбранный трек играет в цикле
        /// </summary>
        Index = 0,

        /// <summary>
        /// После окончания трека запускается случайный, недавно игравшие не выбираются
        /// </summary>
        Random = 1,

        /// <summary>
        /// Трек играет один раз
        /// </summary>
        OneShot = 2,
    }
}
