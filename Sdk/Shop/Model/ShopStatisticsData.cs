using System.Collections.Generic;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Append-only журнал событий покупок и счётчик sequence. Сейв-скоупный модуль:
    /// часть тела сейва (слот «GameSave»), поэтому загрузка более раннего слота откатывает
    /// и журнал, и счётчик — это принятое поведение (список покупок имеет смысл только в рамках прохождения).
    ///
    /// [POCO] не требуется явно — он унаследован от GameModel.IGameData и распространяется на реализации.
    /// Публичный конструктор без параметров обязателен: ComplexModel.Init() собирает реализации
    /// IGameData рефлексией и берёт только типы с таким конструктором.
    /// </summary>
    public class ShopStatisticsData : GameModel.IGameData
    {
        /// <summary>История событий текущего прохождения. Только пополняется.</summary>
        public List<ShopTransactionEvent> Events { get; internal set; } = new();

        /// <summary>Монотонный счётчик sequence, сквозной по журналу слота.</summary>
        public long Sequence { get; internal set; }
    }
}
