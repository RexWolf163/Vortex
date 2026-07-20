using System;
using System.Collections.Generic;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Controllers
{
    public partial class ShopController
    {
        public IReadOnlyList<ShopPurchase> GetOpen() => Collect(p => !p.IsClosed);

        public IReadOnlyList<ShopPurchase> GetReady() => Collect(p => p.State == PurchaseState.Ready);

        public IReadOnlyList<ShopPurchase> GetStuck() => Collect(p => p.State == PurchaseState.Failed);

        public ShopPurchase GetPurchase(string purchaseGuid) => Fold(purchaseGuid);

        public IReadOnlyList<ShopTransactionEvent> GetPurchaseHistory(string purchaseGuid)
        {
            EnsureIndex();
            if (!string.IsNullOrEmpty(purchaseGuid) && _eventsByGuid.TryGetValue(purchaseGuid, out var list))
                return new List<ShopTransactionEvent>(list);
            return new List<ShopTransactionEvent>();
        }

        public IReadOnlyList<ShopTransactionEvent> GetHistory()
        {
            var journal = Journal;
            return journal != null
                ? new List<ShopTransactionEvent>(journal.Events)
                : new List<ShopTransactionEvent>();
        }

        /// <summary>
        /// Свёртки покупок по предикату, в порядке первого появления purchaseGuid. Через индекс —
        /// O(число покупок), а не O(журнал²).
        /// </summary>
        private List<ShopPurchase> Collect(Func<ShopPurchase, bool> predicate)
        {
            EnsureIndex();
            var result = new List<ShopPurchase>();
            foreach (var guid in _order)
                if (_index.TryGetValue(guid, out var fs) && predicate(fs.Purchase))
                    result.Add(fs.Purchase);
            return result;
        }
    }
}
