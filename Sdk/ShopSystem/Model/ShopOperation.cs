#if USING_VORTEX_SHOP
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.ShopSystem.Controllers;

namespace Vortex.Sdk.ShopSystem.Model
{
    /// <summary>
    /// Рантайм-модель одной покупки: свёртка её текущего состояния (реактивный <see cref="State"/>) и
    /// опорные значения сделки. Живёт в индексе <c>ShopOperations.Operations</c>; источник истины —
    /// журнал событий, из которого модель восстанавливается при загрузке. <see cref="State"/> закрыт
    /// на владельца-контроллер (менять только через него).
    /// </summary>
    public class ShopOperation
    {
        /// <summary>Закрывает <see cref="State"/> на ключ контроллера — извне состояние не поменять.</summary>
        public ShopOperation()
        {
            this.LockData();
        }

        /// <summary>
        /// Глобальный идентификатор покупки, генерируется при формировании заказа.
        /// </summary>
        public string PurchaseGuid { get; internal set; }

        /// <summary>
        /// ID Товара в БД
        /// </summary>
        public string ItemGuid { get; internal set; }

        /// <summary>
        /// Состояние проводки.
        /// </summary>
        public EnumData<PurchaseState> State { get; internal set; } = new(PurchaseState.Ordered);

        /// <summary>
        /// Запрошенное количество "пачек" товара.
        /// </summary>
        public int RequestedCount { get; internal set; }

        /// <summary>
        /// Опорное значение, ушедшее в оплату (от <see cref="Logics.PaymentLogic"/>). Не универсальная
        /// сумма — определяется логикой оплаты товара и множителем <see cref="RequestedCount"/>.
        /// </summary>
        public int PayValue { get; internal set; }

        /// <summary>
        /// Опорное значение, ушедшее в выдачу (от <see cref="Logics.DeliveryLogic"/>). Не универсальное —
        /// определяется логикой выдачи товара и множителем <see cref="RequestedCount"/>.
        /// </summary>
        public int BuyValue { get; internal set; }
    }
}
#endif