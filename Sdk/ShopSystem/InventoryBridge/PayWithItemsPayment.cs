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
    /// Оплата предметами-валютой из торгующего инвентаря. Валюта — обычный предмет из БД (монеты,
    /// ключи, лягушки — неважно); цена <see cref="price"/> списывается за пачку с множителем
    /// запрошенных пачек. Списание и возврат идут сквозь стеки: сотня монет в одном стеке считается
    /// за сто.
    /// </summary>
    [Serializable, InfoBox("Оплата предметами-валютой из торгующего инвентаря.")]
    public class PayWithItemsPayment : PaymentLogic
    {
        [SerializeField, DbRecord(typeof(ItemModel), RecordTypes.MultiInstance)]
        private string currencyGuid;

        [SerializeField, Min(0)] private int price;

        public override int GetCount() => price;

        public override UniTask<bool> CanPay(string guid, int count, CancellationToken ct)
        {
            var inventory = TradingInventory.Resolve();
            return UniTask.FromResult(inventory != null && inventory.CountOf(currencyGuid) >= price * count);
        }

        public override UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct)
        {
            var inventory = TradingInventory.Resolve();
            if (inventory == null)
                return UniTask.FromResult(false);

            var total = price * operation.RequestedCount;
            operation.PayValue = total;
            return UniTask.FromResult(inventory.RemoveCount(currencyGuid, total));
        }

        /// <summary>
        /// Возврат валюты. Компенсация обязана доиграть; если инвентарь не принял (переполнен),
        /// возвращаем <c>false</c> — покупка честно уйдёт в Failed на ручной разбор, а не «потеряет»
        /// валюту молчаливым успехом.
        /// </summary>
        public override UniTask<bool> MakeRefund(ShopOperation operation)
        {
            var inventory = TradingInventory.Resolve();
            if (inventory == null)
                return UniTask.FromResult(false);

            return UniTask.FromResult(inventory.AddCount(currencyGuid, price * operation.RequestedCount));
        }
    }
}
#endif
