#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP
using System;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.InventoryBridge
{
    /// <summary>
    /// Точка разрешения «какой инвентарь сейчас торгует». Магазин не знает, где живёт инвентарь
    /// игрока, — он спрашивает событием, а отвечает тот, кто знает (L4: из своего IGameData-модуля,
    /// активного персонажа, из чего угодно). Так бридж остаётся переиспользуемым, а обе стороны
    /// сделки — оплата и выдача — работают с одним инвентарём: у контроллера магазина один активный
    /// процесс, поэтому «текущий торгующий инвентарь» однозначен.
    ///
    /// Мутирующие шаги сделки (оплата, выдача, возврат) резолвят инвентарь через
    /// <see cref="GetInventory(Vortex.Sdk.ShopSystem.Model.ShopOperation)"/> — он фиксирует ответ на всю покупку по её PurchaseGuid,
    /// поэтому даже недетерминированный подписчик не разведёт валюту и товар по разным инвентарям.
    /// Кэш — один слот: у контроллера магазина один активный процесс, покупки не перекрываются.
    /// Предпроверки (<c>CanPay</c>/<c>CanDelivery</c>) резолвят напрямую <see cref="GetInventory"/>:
    /// они не мутируют, привязывать их не нужно.
    ///
    /// Дисциплина подписчика: один подписчик (не «последний выигрывает»), обработчик — чистый
    /// дешёвый запрос без побочных эффектов (за покупку событие поднимается несколько раз).
    /// </summary>
    public static class TradingInventory
    {
        /// <summary>Запрос торгующего инвентаря. Подписчик заполняет <see cref="InventoryRequest.Inventory"/>.</summary>
        public static event Action<InventoryRequest> OnRequested;

        private static string _boundPurchase;
        private static Inventory _boundInventory;

        /// <summary>
        /// Сброс привязки на границе сессии: иначе статик держал бы ссылку на инвентарь прошлого
        /// прохождения (меню → загрузка) до первой покупки в новом. Внутри сессии слот и так
        /// перетирается каждой новой покупкой по её PurchaseGuid.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bootstrap()
        {
            GameController.OnNewGame -= Reset;
            GameController.OnNewGame += Reset;
            GameController.OnLoadGame -= Reset;
            GameController.OnLoadGame += Reset;
        }

        private static void Reset()
        {
            _boundPurchase = null;
            _boundInventory = null;
        }

        /// <summary>Запросить текущий торгующий инвентарь. <c>null</c>, если ответить некому.</summary>
        public static Inventory GetInventory()
        {
            var request = new InventoryRequest();
            OnRequested.Fire(request);
            return request.Inventory;
        }

        /// <summary>
        /// Разрешить инвентарь, зафиксированный на покупку: первый вызов для данного PurchaseGuid
        /// резолвит через событие и запоминает, последующие возвращают тот же инвентарь. Так оплата,
        /// выдача и возврат одной сделки гарантированно работают с одним инвентарём, даже если ответ
        /// подписчика между шагами изменился бы.
        /// </summary>
        public static Inventory GetInventory(ShopOperation operation)
        {
            if (operation.PurchaseGuid == _boundPurchase)
                return _boundInventory;

            _boundPurchase = operation.PurchaseGuid;
            _boundInventory = GetInventory();
            return _boundInventory;
        }
    }
}
#endif