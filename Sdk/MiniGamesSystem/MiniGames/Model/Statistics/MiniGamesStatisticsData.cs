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
        private Dictionary<string, MiniGameStatisticData> _index;

        public Dictionary<string, MiniGameStatisticData> Index
        {
            get => _index ??= new Dictionary<string, MiniGameStatisticData>();
            internal set => _index = value;
        }
    }
}