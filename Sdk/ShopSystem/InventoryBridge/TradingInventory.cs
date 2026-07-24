#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP
using System;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Sdk.InventorySystem.Model;

namespace Vortex.Sdk.ShopSystem.InventoryBridge
{
    /// <summary>
    /// Точка разрешения «какой инвентарь сейчас торгует». Магазин не знает, где живёт инвентарь
    /// игрока, — он спрашивает событием, а отвечает тот, кто знает (L4: из своего IGameData-модуля,
    /// активного персонажа, из чего угодно). Так бридж остаётся переиспользуемым, а обе стороны
    /// сделки — оплата и выдача — работают с одним инвентарём: у контроллера магазина один активный
    /// процесс, поэтому «текущий торгующий инвентарь» однозначен.
    /// </summary>
    public static class TradingInventory
    {
        /// <summary>Запрос торгующего инвентаря. Подписчик заполняет <see cref="InventoryRequest.Inventory"/>.</summary>
        public static event Action<InventoryRequest> OnRequested;

        /// <summary>Разрешить текущий торгующий инвентарь. <c>null</c>, если ответить некому.</summary>
        public static Inventory Resolve()
        {
            var request = new InventoryRequest();
            OnRequested.Fire(request);
            return request.Inventory;
        }
    }
}
#endif
