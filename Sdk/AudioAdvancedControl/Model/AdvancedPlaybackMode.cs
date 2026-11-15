namespace Vortex.Sdk.AudioAdvancedControl.Model
{
    /// <summary>
    /// Режим проигрывания списка клипов
    /// </summary>
    public enum AdvancedPlaybackMode
    {
        /// <summary>
        /// Треки играют по списку: после окончания трека запускается следующий, после последнего — первый
        /// </summary>
        Index = 0,

        /// <summary>
        /// После окончания трека запускается случайный, недавно игравшие не выбираются
        /// </summary>
        Random = 1,

        /// <summary>
        /// Трек играет один раз и останавливается
        /// </summary>
        OneShot = 2,
    }
}
