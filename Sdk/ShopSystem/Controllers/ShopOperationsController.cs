#if USING_VORTEX_SHOP
using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.Controllers
{
    public static class ShopOperationsController
    {
        public static event Action OnLoadDataComplete;

        private static readonly object Key = new();

        private static void SubscribeLifecycle()
        {
            GameController.OnNewGame += LockData;
            GameController.OnLoadGame += LockData;
        }

        private static void LockData()
        {
            var data = GameController.Get<ShopOperations>();
            data.Events.SetOwner(Key);
            data.Transactions = new Dictionary<string, ListData<ShopTransactionEvent>>();
            data.Operations = new Dictionary<string, ShopOperation>();

            var transactions = data.Events.GetList();
            var hasError = false;
            for (var i = 0; i < transactions.Count; i++)
            {
                var transaction = transactions[i];

                if (!hasError && transaction.Sequence != i)
                {
                    Debug.LogError(
                        "[ShopOperationsController] Нарушена нумерация транзакций, возможно данные повреждены!");
                    hasError = true;
                }

                //Индексация транзакций
                if (!data.Transactions.ContainsKey(transaction.PurchaseGuid))
                {
                    var list = new ListData<ShopTransactionEvent>();
                    list.SetOwner(Key);
                    data.Transactions.Add(transaction.PurchaseGuid, list);
                }

                data.Transactions[transaction.PurchaseGuid].Add(transaction, Key);

                //Индексация операций
                if (data.Operations.ContainsKey(transaction.PurchaseGuid))
                {
                    data.Operations[transaction.PurchaseGuid].State.Set(transaction.Type, Key);
                }
                else
                {
                    var operation = new ShopOperation
                    {
                        PurchaseGuid = transaction.PurchaseGuid,
                        BuyValue = transaction.BuyValue,
                        ItemGuid = transaction.ItemGuid,
                        PayValue = transaction.PayValue,
                        RequestedCount = transaction.RequestedCount,
                    };
                    operation.State.Set(transaction.Type, Key);
                    data.Operations[transaction.PurchaseGuid] = operation;
                }
            }

            OnLoadDataComplete?.Invoke();

            var opened = ShopOperationsBus.GetOpen();
            foreach (var operation in opened)
                ShopBus.RestoreOperation(operation);
        }

        internal static void LockData(this ShopOperation operation)
        {
            operation.State.SetOwner(Key);
        }

        internal static void SetState(this ShopOperation operation, PurchaseState state)
        {
            operation.State.Set(state, Key);
        }
    }
}
#endif