using System;
using Cysharp.Threading.Tasks;
using Vortex.Sdk.Shop.Model;
using Vortex.Sdk.Shop.Presets;

namespace Vortex.Sdk.Shop
{
    /// <summary>
    /// Контракт транзакционного движка магазина. Проводка и доступ к каталогу.
    /// Асинхронный публичный вход оправдан длительностью оплаты; синхронный вариант — тонкая
    /// обёртка над ним. Состояние покупок пишет только контроллер, логики докладывают исход
    /// вызовом его методов (через scoped-контексты).
    /// </summary>
    public interface IShopController
    {
        bool IsInitialized { get; }

        /// <summary>Инициализация настройками движка. Каталог подтягивается из Database лениво.</summary>
        void Init(ShopSettings settings);

        void Cleanup();

        // --- каталог (данные проводки) ---

        /// <summary>Товар по guid. Неизвестный guid → пустой товар (заглушка без логик), не null (Inv-5).</summary>
        ShopItemRecord GetItem(string itemGuid);

        // --- проводка ---

        /// <summary>Покупка. Возвращает финальный исход; для UI-await. Открытый исход (Ready/Pending) — тоже здесь.</summary>
        UniTask<ShopResult> Buy(string itemGuid, int count);

        /// <summary>Синхронный fire-and-forget враппер над <see cref="Buy"/> (Forget + лог исключений).</summary>
        void BuyForget(string itemGuid, int count);

        /// <summary>
        /// Подтверждение получения из Ready. Возвращает синхронный отказ: PurchaseBusy при занятости
        /// покупки (Inv-8) или null (принято / не в Ready — нечего подтверждать). Сама выдача асинхронна.
        /// </summary>
        ShopRefusal? ConfirmDelivery(string purchaseGuid);

        /// <summary>Повтор выдачи из Pending. Семантика возврата — как у <see cref="ConfirmDelivery"/>.</summary>
        ShopRefusal? RetryDelivery(string purchaseGuid);

        /// <summary>
        /// Возобновление зависшего Ordered после перезапуска: перевызывает оплату по тому же
        /// purchaseGuid для реконсиляции статуса. Симметрично Retry/Confirm. PurchaseBusy при
        /// занятости, null если покупка не в Ordered.
        /// </summary>
        ShopRefusal? ResumeOrder(string purchaseGuid);

        /// <summary>Ручная отмена открытой покупки с возвратом оплаты. PurchaseBusy при занятости, иначе null.</summary>
        ShopRefusal? CancelWithRefund(string purchaseGuid);

        // --- события проводки ---

        /// <summary>Состояние покупки изменилось. Аргументы: purchaseGuid, новое состояние.</summary>
        event Action<string, PurchaseState> OnPurchaseStateChanged;

        /// <summary>Покупка закрыта терминальным событием.</summary>
        event Action<ShopPurchase> OnPurchaseClosed;
    }
}
