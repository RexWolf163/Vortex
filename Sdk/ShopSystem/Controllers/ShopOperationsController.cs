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
    /// <summary>
    /// Владелец owner-ключа данных магазина. На старте новой игры/загрузки закрывает журнал на ключ,
    /// пересобирает рантайм-индексы (Transactions/Operations) из журнала и запускает восстановление
    /// открытых покупок. Единственная точка мутации данных — через ключ (extension-методы ниже).
    /// </summary>
    public static class ShopOperationsController
    {
        /// <summary>Индексы пересобраны, <see cref="ShopOperations"/> готов к использованию.</summary>
        public static event Action OnLoadDataComplete;

        /// <summary>Owner-ключ: только код с доступом к нему может мутировать журнал и состояния операций.</summary>
        private static readonly object Key = new();

        /// <summary>Подписка на жизненный цикл игры: закрытие/пересборку данных на NewGame и OnLoad.</summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            GameController.OnNewGame += LockData;
            GameController.OnLoadGame += LockData;
        }

        /// <summary>
        /// Закрывает журнал на owner-ключ и пересобирает индексы <c>Transactions</c>/<c>Operations</c>
        /// свёрткой событий (с проверкой сквозной нумерации). По завершении поднимает
        /// <see cref="OnLoadDataComplete"/> и доигрывает открытые покупки.
        /// </summary>
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

        /// <summary>Закрывает <see cref="ShopOperation.State"/> операции на owner-ключ (вызывается из её конструктора).</summary>
        internal static void LockData(this ShopOperation operation)
        {
            operation.State.SetOwner(Key);
        }

        /// <summary>Смена состояния операции через owner-ключ — единственный разрешённый путь мутации.</summary>
        internal static void SetState(this ShopOperation operation, PurchaseState state)
        {
            operation.State.Set(state, Key);
        }

        /// <summary>
        /// Регистрация события: добавляет его в журнал <c>Events</c> и в per-guid индекс (создавая
        /// залоченный список и регистрируя операцию при первом событии покупки). Всё — через owner-ключ.
        /// </summary>
        internal static void RegistrationEvent(this ShopTransactionEvent transactionEvent, ShopOperation operation)
        {
            var data = GameController.Get<ShopOperations>();
            data.Events.Add(transactionEvent, Key);

            if (!data.Transactions.ContainsKey(operation.PurchaseGuid))
            {
                data.Transactions[operation.PurchaseGuid] = new ListData<ShopTransactionEvent>();
                data.Transactions[operation.PurchaseGuid].SetOwner(Key);
                data.Operations[operation.PurchaseGuid] = operation;
            }

            data.Transactions[operation.PurchaseGuid].Add(transactionEvent, Key);
        }
    }
}
#endif