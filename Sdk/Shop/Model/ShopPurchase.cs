namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Свёртка одной покупки в её текущем состоянии. Не хранимая и не сохраняемая модель:
    /// собирается исключительно сворачиванием событий журнала по purchaseGuid
    /// (документированный путь резолвции). Строится по запросу.
    /// </summary>
    public readonly struct ShopPurchase
    {
        public ShopPurchase(string purchaseGuid, string itemGuid, int requestedCount,
            PurchaseState state, int payValue, int buyValue)
        {
            PurchaseGuid = purchaseGuid;
            ItemGuid = itemGuid;
            RequestedCount = requestedCount;
            State = state;
            PayValue = payValue;
            BuyValue = buyValue;
        }

        public string PurchaseGuid { get; }
        public string ItemGuid { get; }
        public int RequestedCount { get; }

        /// <summary>Текущее состояние — тип последнего события покупки.</summary>
        public PurchaseState State { get; }

        public int PayValue { get; }
        public int BuyValue { get; }

        /// <summary>Покупка закрыта ⟺ последнее состояние терминально (Inv-1).</summary>
        public bool IsClosed =>
            State is PurchaseState.Delivered or PurchaseState.Refunded
                or PurchaseState.Cancelled or PurchaseState.Failed;

        /// <summary>Валидность (для пустой свёртки, когда покупка не найдена).</summary>
        public bool Exists => !string.IsNullOrEmpty(PurchaseGuid);
    }
}
