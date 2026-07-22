#if USING_VORTEX_SHOP
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Sdk.ShopSystem.Model
{
    /// <summary>
    /// Модуль игровых данных магазина (<see cref="Core.GameCore.GameModel.IGameData"/>). Лежит в комплексной
    /// модели, поэтому поля доступны извне — журнал <see cref="Events"/> закрыт на владельца-контроллер
    /// (ListData с owner-ключом), а рантайм-индексы <see cref="Transactions"/>/<see cref="Operations"/>
    /// не сериализуются и пересобираются из журнала при загрузке.
    /// </summary>
    public class ShopOperations : Core.GameCore.GameModel.IGameData
    {
        /// <summary>
        /// Append-only журнал событий покупок — источник истины, сохраняется в тело сейва.
        /// Закрыт на owner-ключ контроллера: мутация только через него.
        /// </summary>
        public ListData<ShopTransactionEvent> Events { get; internal set; } = new();

        /// <summary>
        /// Рантайм-индекс: события покупки по её PurchaseGuid. Не сериализуется — пересобирается
        /// из <see cref="Events"/> при загрузке.
        /// </summary>
        internal Dictionary<string, ListData<ShopTransactionEvent>> Transactions { get; set; }

        /// <summary>
        /// Рантайм-индекс: свёрнутая операция по PurchaseGuid. Не сериализуется — пересобирается
        /// из <see cref="Events"/> при загрузке.
        /// </summary>
        internal Dictionary<string, ShopOperation> Operations { get; set; } = new();

        /// <summary>
        /// Рантайм-индекс: свёрнутая операция по ItemGuid. Не сериализуется — пересобирается
        /// из <see cref="Events"/> при загрузке.
        /// </summary>
        internal Dictionary<string, ListData<ShopOperation>> GoodsOperations { get; set; } = new();
    }
}
#endif