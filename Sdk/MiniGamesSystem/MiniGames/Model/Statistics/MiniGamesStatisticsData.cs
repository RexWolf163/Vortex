using System.Collections.Generic;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Model.Statistics
{
    /// <summary>
    /// Модель данных
    /// Индекс статистики запусков, побед и т.п. для миниигр
    /// </summary>
    public class MiniGamesStatisticsData : GameModel.IGameData
    {
        internal Dictionary<string, MiniGameStatisticData> index;

        public IReadOnlyDictionary<string, MiniGameStatisticData> Index =>
            index ??= new Dictionary<string, MiniGameStatisticData>();
    }
}