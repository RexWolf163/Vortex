namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Финальный исход операции <c>Buy</c>. Отдельный тип от <see cref="ShopRefusal"/> сознательно:
    /// отказ предпроверки и итог сделки нельзя перепутать по типу. Синхронная предпроверка,
    /// не начавшая покупку, отдаёт результат с <see cref="Refusal"/>; начатая сделка — с итоговой
    /// свёрткой <see cref="Purchase"/>.
    /// </summary>
    public readonly struct ShopResult
    {
        private ShopResult(bool started, ShopRefusal? refusal, ShopPurchase purchase)
        {
            Started = started;
            Refusal = refusal;
            Purchase = purchase;
        }

        /// <summary>Была ли заведена покупка (пройдены предпроверки и эмитнут Ordered).</summary>
        public bool Started { get; }

        /// <summary>
        /// Причина, если начать не удалось (<see cref="Started"/> == false — отказ предпроверки),
        /// либо причина рантайм-отмены/провала уже начатой сделки (Started == true, покупка Cancelled).
        /// null, когда сделка идёт штатно (Delivered / Ready / Pending). Различать по <see cref="Started"/>.
        /// </summary>
        public ShopRefusal? Refusal { get; }

        /// <summary>Итоговая свёртка покупки, если она была начата.</summary>
        public ShopPurchase Purchase { get; }

        /// <summary>Отказ предпроверки — покупка не заводилась, журнал не писался.</summary>
        public static ShopResult Refused(ShopRefusal refusal) => new(false, refusal, default);

        /// <summary>
        /// Сделка начата; исход — в свёртке (может быть открытой: Ready/Pending). При рантайм-отмене
        /// причина передаётся в <paramref name="reason"/> (например, LogicRejected).
        /// </summary>
        public static ShopResult Completed(ShopPurchase purchase, ShopRefusal? reason = null) =>
            new(true, reason, purchase);
    }
}
