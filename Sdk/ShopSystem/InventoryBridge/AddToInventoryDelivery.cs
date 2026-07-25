#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.InventorySystem.Controllers;
using Vortex.Sdk.ItemsSystem.Model;
using Vortex.Sdk.ShopSystem.Model;
using Vortex.Sdk.ShopSystem.Model.Logics;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.ShopSystem.InventoryBridge
{
    /// <summary>
    /// Выдача купленного предмета в торгующий инвентарь. Товар — предмет из БД (пресет предмета),
    /// выдаётся в количестве <see cref="amount"/> на пачку с учётом множителя запрошенных пачек.
    /// Инвентарь-получатель резолвится через <see cref="TradingInventory"/>, поэтому логика не знает,
    /// чей это инвентарь.
    /// </summary>
    [Serializable, InfoBox("Выдача купленного предмета в торгующий инвентарь.")]
    public class AddToInventoryDelivery : DeliveryLogic
    {
        [SerializeField, DbRecord(typeof(ItemModel), RecordTypes.MultiInstance)]
        private string itemGuid;

        [SerializeField, Min(1)] private int amount = 1;

        public override int GetCount() => amount;

        public override string GetItemGuid() => itemGuid;

        /// <summary>
        /// Выдать можно, если торгующий инвентарь разрешился И в него влезает выдаваемое количество.
        /// Вместимость проверяется тихой пробой (<c>CanAdd</c>: без создания реального предмета, без
        /// события и без сдвига оси) по верификаторам инвентаря.
        ///
        /// Проверка идёт по текущему состоянию инвентаря, то есть валюта, которой ещё не заплачено,
        /// занимает своё место. Если платят предметами из того же инвентаря и на момент проверки
        /// массы/объёма не хватает, покупка отклоняется до оплаты — это стандартная механика «нет
        /// места», а не баг: игрок сперва освобождает место, потом покупает.
        /// </summary>
        public override UniTask<bool> CanDelivery(string guid, int count, CancellationToken ct)
        {
            var inventory = TradingInventory.Resolve();
            return UniTask.FromResult(inventory != null && inventory.CanAdd(itemGuid, (long)amount * count));
        }

        public override UniTask<bool> MakeDelivery(ShopOperation operation, CancellationToken ct)
        {
            var inventory = TradingInventory.Resolve(operation);
            if (inventory == null)
                return UniTask.FromResult(false);

            var total = (long)amount * operation.RequestedCount;
            operation.BuyValue = total;
            return UniTask.FromResult(inventory.AddCount(itemGuid, total));
        }
    }
}
#endif
