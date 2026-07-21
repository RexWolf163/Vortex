#if USING_VORTEX_SHOP
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Sdk.ShopSystem.Model
{
    /// <summary>
    /// Данные индекса хранятся в комплексной модели и поэтому доступны для недопустимых изменений снаружи.
    /// Поэтому, с учетом важности данных, используется ListData с замком контроллера
    /// </summary>
    public class ShopOperations : Core.GameCore.GameModel.IGameData
    {
        /// <summary>
        /// индекс событий-транзакций по их номеру
        /// </summary>
        public ListData<ShopTransactionEvent> Events { get; internal set; } = new();

        /// <summary>
        /// индекс событий-транзакций по guid покупки
        /// </summary>
        internal Dictionary<string, ListData<ShopTransactionEvent>> Transactions { get; set; }

        /// <summary>
        /// индекс операций покупки по guid покупки
        /// </summary>
        internal Dictionary<string, ShopOperation> Operations { get; set; } = new();
    }
}
#endif