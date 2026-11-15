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
        /// Следующий по списку трек играет один раз и останавливается
        /// </summary>
        IndexOneShot = 2,

        /// <summary>
        /// Случайный трек играет один раз и останавливается, недавно игравшие не выбираются
        /// </summary>
        RandomOneShot = 3,
    }
}
