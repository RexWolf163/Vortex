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
    /// Учётная шина магазина (read-only доступ к данным покупок): открытые/готовые/зависшие покупки,
    /// свёртка по guid, история событий. Запись в журнал — внутренняя (<see cref="MakeRecord"/>).
    /// Проводка (покупка/выдача/отмена) — в <see cref="ShopBus"/>.
    /// </summary>
    public static class ShopOperationsBus
    {
        /// <summary>Шлюз готовности: подписки выполнятся после того, как данные загружены и <see cref="Data"/> установлен.</summary>
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;

        /// <summary>Модуль данных магазина текущей сессии. null до первой загрузки/новой игры.</summary>
        public static ShopOperations Data { get; private set; }

        /// <summary>Контроллер записи журнала (stateless, пишет через owner-ключ модели).</summary>
        private static IShopTransactionsController Controller => ShopTransactionsController.Instance;

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

        /// <summary>Зависшие — покупки, закрытые Failed (товар без обязательных логик). Для техподдержки.</summary>
        public static IReadOnlyList<ShopOperation> GetStuck() => Data?.Operations.Values
            .Where(operation => operation.State.Value == PurchaseState.Failed).ToList();

        /// <summary>Свёртка конкретной покупки в её текущем состоянии.</summary>
        public static ShopOperation GetOperation(string purchaseGuid) =>
            Data?.Operations.GetValueOrDefault(purchaseGuid);

        /// <summary>Все события конкретной покупки (сырой срез журнала).</summary>
        public static ListData<ShopTransactionEvent> GetPurchaseHistory(string purchaseGuid) =>
            Data?.Transactions.GetValueOrDefault(purchaseGuid);

        /// <summary>Полная история событий журнала</summary>
        public static ListData<ShopTransactionEvent> GetHistory() => Data?.Events;

        /// <summary>Полная история событий журнала по конкретному товару</summary>
        public static ListData<ShopOperation> GetGoodsHistory(string itemGuid) =>
            Data?.GoodsOperations.GetValueOrDefault(itemGuid);

        /// <summary>Зафиксировать текущее состояние операции новым событием журнала. Возвращает успех записи.</summary>
        /// <param name="operation">Операция, чьё текущее состояние фиксируется.</param>
        internal static bool MakeRecord(ShopOperation operation) => Controller.MakeRecord(operation);

        #region Init

        /// <summary>Подписка на завершение сборки данных: по нему <see cref="Reset"/> подхватывает <see cref="Data"/>.</summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            ShopOperationsController.OnLoadDataComplete += Reset;
        }

        /// <summary>Подхват модуля данных из GameModel после сборки индексов и открытие шлюза готовности.</summary>
        private static void Reset()
        {
            Data = GameController.Get<ShopOperations>();
            OnInitComplete?.Invoke();
        }

        #endregion
    }
}
#endif