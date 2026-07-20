namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Причина отказа, возвращаемая вызывающей стороне при невозможности начать или продолжить покупку.
    /// Отсутствие отказа выражается как <c>null</c> у <see cref="System.Nullable{T}"/>, отдельного
    /// «успешного» значения в перечислении нет.
    /// </summary>
    public enum ShopRefusal
    {
        /// <summary>requestedCount &lt;= 0.</summary>
        InvalidCount,

        /// <summary>Идентификатор не резолвится в каталоге.</summary>
        UnknownItem,

        /// <summary>Предпроверка оплаты отказала.</summary>
        InsufficientResources,

        /// <summary>Предпроверка выдачи отказала.</summary>
        DeliveryUnavailable,

        /// <summary>Существует заказ между Ordered и Paid.</summary>
        OrderInProgress,

        /// <summary>По этой покупке уже выполняется операция.</summary>
        PurchaseBusy,

        /// <summary>У товара нет логик (пустой товар).</summary>
        LogicUnavailable,

        /// <summary>Логика отказала при выполнении.</summary>
        LogicRejected,

        /// <summary>
        /// Движок не может журналировать: журнал (<c>ShopStatisticsData</c>) недоступен — нет активной
        /// игровой сессии / слота, либо тип вырезан managed-stripping в билде. Покупка НЕ начинается и
        /// НЕ списывает: журнал — единый сохраняемый контур (Inv-9), без него оплата прошла бы без следа.
        /// </summary>
        EngineUnavailable
    }
}
