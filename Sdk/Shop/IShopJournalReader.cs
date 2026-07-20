using System;
using System.Collections.Generic;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop
{
    /// <summary>
    /// Контракт учёта — чтение журнала. Отделён от <see cref="IShopController"/> (проводка):
    /// два домена, два контракта. Реализуется тем же контроллером, но шина учёта видит только это.
    /// </summary>
    public interface IShopJournalReader
    {
        IReadOnlyList<ShopPurchase> GetOpen();
        IReadOnlyList<ShopPurchase> GetReady();
        IReadOnlyList<ShopPurchase> GetStuck();
        ShopPurchase GetPurchase(string purchaseGuid);
        IReadOnlyList<ShopTransactionEvent> GetPurchaseHistory(string purchaseGuid);
        IReadOnlyList<ShopTransactionEvent> GetHistory();

        event Action OnJournalUpdated;
    }
}
