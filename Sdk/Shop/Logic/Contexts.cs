using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Logic
{
    /// <summary>
    /// Контекст фазы оплаты, выданный контроллером логике и привязанный к одной покупке.
    /// Логика докладывает исход вызовом метода — контроллер, а не логика, пишет событие журнала.
    /// Scoped по purchaseGuid: логика не может двигать чужую покупку.
    /// </summary>
    public interface IPayContext
    {
        /// <summary>Запрошенное количество единиц.</summary>
        int RequestedCount { get; }

        /// <summary>Оплата подтверждена. payValue — фактически ушедшее значение (для журнала).</summary>
        void Paid(int payValue);

        /// <summary>Оплата не прошла. Покупка закрывается как Cancelled, ничего не списано.</summary>
        void Cancelled(ShopRefusal reason);
    }

    /// <summary>
    /// Контекст фазы выдачи. Логика выбирает исход: выдано, отложено на подтверждение игроком,
    /// удержано для повтора, либо провал. При провале контроллер применяет глобальную политику фолбэка.
    /// </summary>
    public interface IBuyContext
    {
        int RequestedCount { get; }

        /// <summary>Сколько было оплачено — доступно логике для расчёта выдачи.</summary>
        int PayValue { get; }

        /// <summary>Выдано. buyValue — фактически выданное значение (для журнала). Покупка закрывается.</summary>
        void Delivered(int buyValue);

        /// <summary>Ожидает подтверждения игроком. Покупка переводится в Ready, автовыдачи нет.</summary>
        void Ready();

        /// <summary>Удержать открытой для повтора. Покупка переводится в Pending.</summary>
        void Pending();

        /// <summary>Провал выдачи. Контроллер применит глобальную политику фолбэка.</summary>
        void Failed(ShopRefusal reason);
    }

    /// <summary>
    /// Контекст фазы возврата. Провал возврата не порождает терминального события (Inv-6):
    /// покупка остаётся открытой, повтор инициирует внешний механизм.
    /// </summary>
    public interface IRefundContext
    {
        /// <summary>Сколько нужно вернуть.</summary>
        int PayValue { get; }

        /// <summary>Возврат выполнен. Покупка закрывается как Refunded.</summary>
        void Refunded();

        /// <summary>Возврат не прошёл. Покупка остаётся открытой, терминальное событие не пишется (Inv-6).</summary>
        void Failed(ShopRefusal reason);
    }
}
