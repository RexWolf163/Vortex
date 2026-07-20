using System;
using System.Collections.Generic;
using Vortex.Sdk.Shop.Controllers;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Bus
{
    /// <summary>
    /// Домен «учёт»: доступ только на чтение к журналу покупок. Отделён от проводки, потому что
    /// это отдельная зона ответственности с другими потребителями (аналитика, техподдержка, UI-история).
    ///
    /// Перечни разведены сознательно: готовые к получению (Ready) отдаются отдельно от требующих
    /// внимания (Failed), чтобы штатные неполученные награды не засоряли разбор сбоев.
    /// </summary>
    public static class ShopJournal
    {
        private static IShopJournalReader Reader => ShopController.Instance;

        /// <summary>Открытые (не-терминальные) покупки: Ordered / Paid / Ready / Pending.</summary>
        public static IReadOnlyList<ShopPurchase> GetOpen() => Reader.GetOpen();

        /// <summary>Готовые к получению — покупки в состоянии Ready.</summary>
        public static IReadOnlyList<ShopPurchase> GetReady() => Reader.GetReady();

        /// <summary>Зависшие — покупки, закрытые Failed. Для техподдержки и ручного разбора.</summary>
        public static IReadOnlyList<ShopPurchase> GetStuck() => Reader.GetStuck();

        /// <summary>Свёртка конкретной покупки в её текущем состоянии.</summary>
        public static ShopPurchase GetPurchase(string purchaseGuid) => Reader.GetPurchase(purchaseGuid);

        /// <summary>Все события конкретной покупки (сырой срез журнала).</summary>
        public static IReadOnlyList<ShopTransactionEvent> GetPurchaseHistory(string purchaseGuid) =>
            Reader.GetPurchaseHistory(purchaseGuid);

        /// <summary>Полная история событий журнала текущего слота.</summary>
        public static IReadOnlyList<ShopTransactionEvent> GetHistory() => Reader.GetHistory();

        /// <summary>Журнал пополнился новым событием.</summary>
        public static event Action OnJournalUpdated
        {
            add => Reader.OnJournalUpdated += value;
            remove => Reader.OnJournalUpdated -= value;
        }
    }
}
