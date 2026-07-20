using System;
using Cysharp.Threading.Tasks;
using Vortex.Sdk.Shop.Controllers;
using Vortex.Sdk.Shop.Model;
using Vortex.Sdk.Shop.Presets;

namespace Vortex.Sdk.Shop.Bus
{
    /// <summary>
    /// Домен «проводка»: публичная точка входа в транзакционный движок и доступ к каталогу.
    /// Учёт (чтение журнала) вынесен в отдельную шину <see cref="ShopJournal"/> — два домена, две шины.
    /// Шина логики не содержит: только доступ, форвардинг операций и события.
    ///
    /// Названа ShopBus, а не Shop: тип с именем сегмента неймспейса (Vortex.Sdk.Shop) даёт коллизию.
    /// Совпадает с каноном шин Vortex (RewardBus, MapLevelsBus).
    /// </summary>
    public static class ShopBus
    {
        /// <summary>
        /// Упрощенная форма получения контроллера, без выбора, но с возможностью масштабирования
        /// под вариативные контроллеры
        /// </summary>
        internal static IShopController Controller => ShopController.Instance;

        public static bool IsReady => ShopController.Instance.IsInitialized;

        /// <summary>Инициализация движка настройками. Вызывается bootstrap-кодом проекта.</summary>
        public static void Init(ShopSettings settings) => Controller.Init(settings);

        // --- каталог ---
        public static ShopItemRecord GetItem(string itemGuid) => Controller.GetItem(itemGuid);

        // --- проводка ---
        public static UniTask<ShopResult> Buy(string itemGuid, int count) => Controller.Buy(itemGuid, count);
        public static void BuyForget(string itemGuid, int count) => Controller.BuyForget(itemGuid, count);
        public static ShopRefusal? ConfirmDelivery(string purchaseGuid) => Controller.ConfirmDelivery(purchaseGuid);
        public static ShopRefusal? RetryDelivery(string purchaseGuid) => Controller.RetryDelivery(purchaseGuid);
        public static ShopRefusal? ResumeOrder(string purchaseGuid) => Controller.ResumeOrder(purchaseGuid);
        public static ShopRefusal? CancelWithRefund(string purchaseGuid) => Controller.CancelWithRefund(purchaseGuid);

        // --- события ---
        public static event Action<string, PurchaseState> OnPurchaseStateChanged
        {
            add => Controller.OnPurchaseStateChanged += value;
            remove => Controller.OnPurchaseStateChanged -= value;
        }

        public static event Action<ShopPurchase> OnPurchaseClosed
        {
            add => Controller.OnPurchaseClosed += value;
            remove => Controller.OnPurchaseClosed -= value;
        }
    }
}