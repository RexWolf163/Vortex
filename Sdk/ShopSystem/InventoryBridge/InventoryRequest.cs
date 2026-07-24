#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP
using Vortex.Sdk.InventorySystem.Model;

namespace Vortex.Sdk.ShopSystem.InventoryBridge
{
    /// <summary>
    /// Пакет запроса торгующего инвентаря: логика оплаты/выдачи поднимает событие с пустым запросом,
    /// подписчик из L4 заполняет <see cref="Inventory"/> тем инвентарём, что сейчас в сделке.
    /// </summary>
    public class InventoryRequest
    {
        /// <summary>Инвентарь сделки. Заполняет подписчик; <c>null</c> — отвечать некому.</summary>
        public Inventory Inventory { get; set; }
    }
}
#endif
