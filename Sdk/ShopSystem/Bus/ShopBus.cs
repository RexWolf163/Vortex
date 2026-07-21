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
    public class ShopBus : SystemController<ShopBus, IShopController>
    {
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;
        private readonly Dictionary<string, ShopItemModel> _shopItems = new();

        public IReadOnlyDictionary<string, ShopItemModel> ShopItems => _shopItems;
        private ShopSettings _settings;
        internal static ShopSettings Settings => Instance._settings;

        protected override void OnDriverConnect()
        {
            Driver.SetIndex(_shopItems);

            _settings = Resources.LoadAll<ShopSettings>("")[0];

            OnInitComplete?.Invoke();
        }

        protected override void OnDriverDisconnect()
        {
            _shopItems.Clear();
        }

        /// <summary>Идёт ли интерактивный процесс. Пред-проверка для UI, чтобы отличать «занято» от прочих отказов.</summary>
        public static bool IsBusy => HasDriver() && Driver.IsBusy;

        public static async UniTask<ShopOperation> Buy(string purchaseGuid, int count) =>
            await Driver.Buy(purchaseGuid, count);

        public static ShopOperation BuyForget(string purchaseGuid, int count) =>
            Driver.BuyForget(purchaseGuid, count);

        public static void RestoreOperation(ShopOperation operation) =>
            Driver.Processing(operation).Forget(Debug.LogException);

        public static void ConfirmDelivery(ShopOperation operation) =>
            Driver.ConfirmDelivery(operation).Forget(Debug.LogException);

        public static void RetryDelivery(ShopOperation operation) =>
            Driver.RetryDelivery(operation).Forget(Debug.LogException);

        public static void CancelWithRefund(ShopOperation operation) =>
            Driver.CancelWithRefund(operation).Forget(Debug.LogException);
    }
}
#endif