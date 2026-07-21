#if USING_VORTEX_SHOP
using System;
using Cysharp.Threading.Tasks;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.ShopSystem.Model;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.ShopSystem.Presets;

namespace Vortex.Sdk.ShopSystem.Bus
{
    /// <summary>
    /// Публичная шина магазина (проводка) — фасад над драйвером <see cref="IShopController"/>: покупка,
    /// подтверждение/повтор выдачи, отмена, доступ к каталогу и настройкам. Учёт (журнал, история,
    /// свёртки покупок) — в <see cref="ShopOperationsBus"/>.
    /// </summary>
    public class ShopBus : SystemController<ShopBus, IShopController>
    {
        /// <summary>Шлюз готовности: подписки выполнятся после подключения драйвера и загрузки настроек.</summary>
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;
        private readonly Dictionary<string, ShopItemModel> _shopItems = new();

        /// <summary>Каталог товаров, доступный только для чтения.</summary>
        public IReadOnlyDictionary<string, ShopItemModel> ShopItems => _shopItems;

        private ShopSettings _settings;

        /// <summary>Настройки магазина (политика AfterpayMode). Внутренний доступ для драйвера.</summary>
        internal static ShopSettings Settings => Instance._settings;

        /// <summary>Подключение драйвера: отдаём ему каталог под заполнение, грузим настройки, открываем шлюз.</summary>
        protected override void OnDriverConnect()
        {
            Driver.SetIndex(_shopItems);

            _settings = Resources.LoadAll<ShopSettings>("")[0];

            OnInitComplete?.Invoke();
        }

        /// <summary>Отключение драйвера: очистка каталога.</summary>
        protected override void OnDriverDisconnect()
        {
            _shopItems.Clear();
        }

        /// <summary>Идёт ли интерактивный процесс. Пред-проверка для UI, чтобы отличать «занято» от прочих отказов.</summary>
        public static bool IsBusy => HasDriver() && Driver.IsBusy;

        /// <summary>Покупка товара по guid (await до терминала/равновесия). См. <see cref="IShopController.Buy"/>.</summary>
        public static async UniTask<ShopOperation> Buy(string purchaseGuid, int count) =>
            await Driver.Buy(purchaseGuid, count);

        /// <summary>Синхронная fire-and-forget покупка. См. <see cref="IShopController.BuyForget"/>.</summary>
        public static ShopOperation BuyForget(string purchaseGuid, int count) =>
            Driver.BuyForget(purchaseGuid, count);

        /// <summary>Доигрывание открытой покупки (используется восстановлением после загрузки).</summary>
        public static void RestoreOperation(ShopOperation operation) =>
            Driver.Processing(operation).Forget(Debug.LogException);

        /// <summary>Подтверждение получения покупки в состоянии Ready.</summary>
        public static void ConfirmDelivery(ShopOperation operation) =>
            Driver.ConfirmDelivery(operation).Forget(Debug.LogException);

        /// <summary>Повтор выдачи покупки в состоянии Pending.</summary>
        public static void RetryDelivery(ShopOperation operation) =>
            Driver.RetryDelivery(operation).Forget(Debug.LogException);

        /// <summary>Отмена покупки: прерывает идущий процесс и возвращает оплату, если она была проведена.</summary>
        public static void CancelWithRefund(ShopOperation operation) =>
            Driver.CancelWithRefund(operation).Forget(Debug.LogException);
    }
}
#endif