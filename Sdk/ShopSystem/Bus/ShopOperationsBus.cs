#if USING_VORTEX_SHOP
using System;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Sdk.ShopSystem.Model;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.ShopSystem.Controllers;

namespace Vortex.Sdk.ShopSystem.Bus
{
    /// <summary>
    /// Шина доступа к данным по транзакциям
    /// </summary>
    public static class ShopOperationsBus
    {
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;

        public static ShopOperations Data { get; private set; }
        private static IShopTransactionsController Controller => new ShopTransactionsController();

        /// <summary>Открытые (не-терминальные) покупки: Ordered / Paid / Ready / Pending.</summary>
        public static IReadOnlyList<ShopOperation> GetOpen()
        {
            if (Data == null)
                return null;

            var list = new List<ShopOperation>();
            foreach (var operation in Data.Operations.Values)
            {
                switch (operation.State.Value)
                {
                    case PurchaseState.Ordered:
                    case PurchaseState.Paid:
                    case PurchaseState.Ready:
                    case PurchaseState.Pending:
                        list.Add(operation);
                        break;
                    case PurchaseState.Delivered:
                    case PurchaseState.Refunded:
                    case PurchaseState.Cancelled:
                    case PurchaseState.Failed:
                    case PurchaseState.NotStarted:
                        break;
                }
            }

            return list;
        }

        /// <summary>Готовые к получению — покупки в состоянии Ready.</summary>
        public static IReadOnlyList<ShopOperation> GetReady() => Data?.Operations.Values
            .Where(operation => operation.State.Value == PurchaseState.Ready).ToList();

        /// <summary>Зависшие — покупки, закрытые Failed. Для техподдержки и ручного разбора.</summary>
        public static IReadOnlyList<ShopOperation> GetStuck() => Data?.Operations.Values
            .Where(operation => operation.State.Value is PurchaseState.Failed or PurchaseState.NotStarted).ToList();

        /// <summary>Свёртка конкретной покупки в её текущем состоянии.</summary>
        public static ShopOperation GetOperation(string purchaseGuid) =>
            Data?.Operations.GetValueOrDefault(purchaseGuid);

        /// <summary>Все события конкретной покупки (сырой срез журнала).</summary>
        public static ListData<ShopTransactionEvent> GetPurchaseHistory(string purchaseGuid) =>
            Data?.Transactions.GetValueOrDefault(purchaseGuid);

        /// <summary>Полная история событий журнала</summary>
        public static ListData<ShopTransactionEvent> GetHistory() => Data.Events;

        /// <summary>
        /// Создать новую запись о текущем состоянии операции покупки
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        internal static bool MakeRecord(ShopOperation operation) => Controller.MakeRecord(operation);

        #region Init

        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            ShopOperationsController.OnLoadDataComplete += Reset;
        }

        private static void Reset()
        {
            Data = GameController.Get<ShopOperations>();
            OnInitComplete?.Invoke();
        }

        #endregion
    }
}
#endif