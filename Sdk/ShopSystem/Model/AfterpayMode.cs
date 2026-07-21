namespace Vortex.Sdk.ShopSystem.Model
{
    public enum AfterpayMode
    {
        /// <summary>Автовыдача. При отказе выдачи — возврат оплаты и закрытие покупки.</summary>
        Rollback,

        /// <summary>Автовыдача. При отказе выдачи — удержание покупки открытой в Pending для внешнего повтора.</summary>
        Pending,

        /// <summary>Автовыдачи нет. Покупка переводится в Ready и ждёт подтверждения игроком.</summary>
        Ready
    }
}