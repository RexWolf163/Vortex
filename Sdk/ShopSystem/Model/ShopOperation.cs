using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.ShopSystem.Controllers;

namespace Vortex.Sdk.ShopSystem.Model
{
    public class ShopOperation
    {
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
        /// Фактическое значение, ушедшее в оплату (от PaymentLogic).
        /// Опорное, не универсальная сумма.
        /// Определяется настройками товара в логике PaymentLogic и множителем RequestedCount
        /// </summary>
        public int PayValue { get; internal set; }

        /// <summary>
        /// Фактическое значение, ушедшее в выдачу (от BuyCaseLogic).
        /// Опорное, не универсальное значение.
        /// Определяется настройками товара в логике PaymentLogic и множителем RequestedCount
        /// </summary>
        public int BuyValue { get; internal set; }
    }
}