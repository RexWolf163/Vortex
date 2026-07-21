#if USING_VORTEX_SHOP
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Sdk.ShopSystem.Model.Logics;

namespace Vortex.Sdk.ShopSystem.Model
{
    /// <summary>
    /// Товар магазина — неизменяемая запись каталога (<see cref="Record"/>, наполняется из пресета).
    /// Обе логики обязательны: их отсутствие делает товар нефункциональным (покупка терминализуется
    /// как <see cref="PurchaseState.Failed"/>). В тело сейва не входит — каталог статичен.
    /// </summary>
    public class ShopItemModel : Record
    {
        /// <summary>
        /// Логика оплаты. Обязательна; для бесплатного товара — отдельная логика с нулевой ценой.
        /// null → товар сломан (покупка → Failed).
        /// </summary>
        public PaymentLogic PaymentLogic { get; protected set; }

        /// <summary>Логика выдачи. Обязательна; null → товар сломан (покупка → Failed).</summary>
        public DeliveryLogic DeliveryLogic { get; protected set; }

        /// <summary>
        /// Скрыть в витрине. Статический флаг дизайнера: товары-компенсации и «удалённые» товары
        /// не должны показываться в обычной витрине.
        /// </summary>
        public bool HiddenInShowcase { get; protected set; }

        /// <summary>Каталог неизменяем и в тело сейва не входит — сохранять нечего.</summary>
        public override string GetDataForSave() => null;

        /// <summary>Каталог неизменяем — восстанавливать нечего.</summary>
        public override void LoadFromSaveData(string data)
        {
        }
    }
}
#endif