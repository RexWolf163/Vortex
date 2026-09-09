using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
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

        /// <summary>
        /// Единственный сериализуемый член модели. [IsPOCO] обязателен: непубличный getter без него
        /// в сериализацию не попадает (см. SerializeController.GetReadablePropertiesList), и вся
        /// статистика миниигр молча не доезжала до сейва. Поле index серилизатор не видит вовсе —
        /// он работает только по свойствам, а публичное Index отсекается отсутствием сеттера.
        /// </summary>
        [IsPOCO]
        private Dictionary<string, MiniGameStatisticData> IndexData
        {
            get => index;
            set => index = value;
        }

        public IReadOnlyDictionary<string, MiniGameStatisticData> Index =>
            index ??= new Dictionary<string, MiniGameStatisticData>();
    }
}