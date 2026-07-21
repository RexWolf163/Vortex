using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.ShopSystem.Controllers;
using Vortex.Sdk.ShopSystem.Model;
using Vortex.Sdk.ShopSystem.Presets;

namespace Vortex.Sdk.ShopSystem.Bus
{
    public class ShopBus : SystemController<ShopBus, IShopController>
    {
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;

        private IShopController Controller => new ShopController();

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

        public static async UniTask<ShopOperation> Buy(string purchaseGuid, int count) =>
            await Instance.Controller.Buy(purchaseGuid, count);

        public static ShopOperation BuyForget(string purchaseGuid, int count) =>
            Instance.Controller.BuyForget(purchaseGuid, count);

        public static void RestoreOperation(ShopOperation operation) =>
            Instance.Controller.NextStep(operation).Forget(Debug.LogException);

        public static void ConfirmDelivery(ShopOperation operation) =>
            Instance.Controller.ConfirmDelivery(operation).Forget(Debug.LogException);

        public static void RetryDelivery(ShopOperation operation) =>
            Instance.Controller.RetryDelivery(operation).Forget(Debug.LogException);
    }
}