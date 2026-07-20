using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Неизменяемое событие журнала — факт одной операции над покупкой. Все поля заполняются
    /// на каждом событии: событие самодостаточно как срез корректных данных в моменте.
    ///
    /// Достраивающие зависимости выражены явно: itemGuid — GUID-ссылка на товар (резолвится
    /// каталогом, неизвестный guid даёт пустой товар, не null); payValue/buyValue — поглощение
    /// (копируются в момент сделки, потому что параметры логик патчатся, а журналу нужна цена
    /// на тот момент).
    ///
    /// Публичный getter + internal setter — попадает в property-based сериализацию SerializeController.
    /// Публичный конструктор без параметров обязателен для десериализации элемента списка.
    /// </summary>
    [POCO]
    public class ShopTransactionEvent
    {
        /// <summary>Сквозной идентификатор покупки, генерируется при формировании заказа.</summary>
        public string PurchaseGuid { get; internal set; }

        /// <summary>Монотонный номер, сквозной по журналу слота. Единственный авторитет порядка событий.</summary>
        public long Sequence { get; internal set; }

        /// <summary>Товар.</summary>
        public string ItemGuid { get; internal set; }

        /// <summary>Тип операции.</summary>
        public PurchaseState Type { get; internal set; }

        /// <summary>Запрошенное количество.</summary>
        public int RequestedCount { get; internal set; }

        /// <summary>Фактическое значение, ушедшее в оплату (от PaymentLogic). Опорное, не универсальная сумма.</summary>
        public int PayValue { get; internal set; }

        /// <summary>Фактическое значение, ушедшее в выдачу (от BuyCaseLogic).</summary>
        public int BuyValue { get; internal set; }

        /// <summary>UTC-секунды (DateTimeOffset.UtcNow.ToUnixTimeSeconds). Часы устройства — недоверенная ось.</summary>
        public long Timestamp { get; internal set; }

        /// <summary>GameController.PlayTime на момент события — «на каком этапе прохождения». Немонотонна между слотами.</summary>
        public long PlaySeconds { get; internal set; }

        /// <summary>GameController.AppTime на момент события — «сколько игрок провёл в приложении».</summary>
        public long AppSeconds { get; internal set; }
    }
}
