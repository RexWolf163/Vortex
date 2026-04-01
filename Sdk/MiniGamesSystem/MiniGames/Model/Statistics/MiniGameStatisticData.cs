namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Model.Statistics
{
    /// <summary>
    /// Статистические данные миниигры
    /// </summary>
    public class MiniGameStatisticData
    {
        /// <summary>
        /// ID миниигры
        /// </summary>
        public string MiniGameKey { get; internal set; }

        /// <summary>
        /// Кол-во побед
        /// </summary>
        public int WinGames { get; internal set; }

        /// <summary>
        /// Кол-во поражений
        /// </summary>
        public int FailGames { get; internal set; }

        /// <summary>
        /// Кол-во запущенных игровых сессий
        /// </summary>
        public int StartedGames { get; internal set; }

        /// <summary>
        /// Кол-во недоигранных игровых сессий
        /// </summary>
        public int UnfinishedGames => StartedGames - WinGames - FailGames;
    }
}