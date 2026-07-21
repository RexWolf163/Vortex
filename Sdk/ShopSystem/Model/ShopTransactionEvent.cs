#if USING_VORTEX_SHOP
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.ShopSystem.Model
{
    /// <summary>
    /// Неизменяемое событие журнала — факт одной операции над покупкой. Все поля заполняются
    /// на каждом событии: событие самодостаточно как срез корректных данных в моменте.
    ///
    /// Публичный getter + internal setter — попадает в property-based сериализацию SerializeController.
    /// Публичный конструктор без параметров обязателен для десериализации элемента списка.
    ///
    /// ВАЖНО: Событие покупки несёт смысл только в моменте игры, с загруженной сессией.
    /// Оно не должно существовать вне игровой сессии. Механика пакета это не поддерживает. 
    /// </summary>
    [POCO]
    public class ShopTransactionEvent
    {
        /// <summary>
        /// Глобальный идентификатор покупки, генерируется при формировании заказа.
        /// </summary>
        public string PurchaseGuid { get; internal set; }

        /// <summary>
        /// Монотонный номер, сквозной по журналу слота.
        /// В нормальных условиях равен индексу в списке.
        /// </summary>
        public long Sequence { get; internal set; }

        /// <summary>
        /// ID Товара в БД
        /// </summary>
        public string ItemGuid { get; internal set; }

        /// <summary>
        /// Тип операции.
        /// </summary>
        public PurchaseState Type { get; internal set; }

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
        /// Фактическое значение, ушедшее в выдачу (от DeliveryLogic).
        /// Опорное, не универсальное значение.
        /// Определяется настройками товара в логике DeliveryLogic и множителем RequestedCount
        /// </summary>
        public int BuyValue { get; internal set; }

        /// <summary>
        /// UTC-секунды (DateTimeOffset.UtcNow.ToUnixTimeSeconds). Часы устройства — недоверенная ось.
        /// </summary>
        public long Timestamp { get; internal set; }

        /// <summary>
        /// GameController.PlayTime на момент события — «на каком этапе прохождения».
        /// Зависит от данных внутри загруженного сейва
        /// </summary>
        public long PlaySeconds { get; internal set; }

        /// <summary>
        /// GameController.AppTime на момент события — «сколько игрок провёл в приложении».
        /// </summary>
        public long AppSeconds { get; internal set; }
    }
}
#endif