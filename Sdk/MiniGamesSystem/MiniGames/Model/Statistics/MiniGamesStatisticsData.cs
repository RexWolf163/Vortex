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

        private Dictionary<string, MiniGameStatisticData> IndexData
        {
            get => index;
            set => index = value;
        }

        public IReadOnlyDictionary<string, MiniGameStatisticData> Index =>
            index ??= new Dictionary<string, MiniGameStatisticData>();
    }
}