using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Model.Statistics
{
    /// <summary>
    /// Статистические данные миниигры
    /// </summary>
    [POCO]
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

        /// <summary>
        /// Значение рекордов (флоат привести к целому, чтобы избежать дрифта)
        /// </summary>
        public int Record { get; internal set; }
    }
}