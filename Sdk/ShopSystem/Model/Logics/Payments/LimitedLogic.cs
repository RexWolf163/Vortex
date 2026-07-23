#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.ShopSystem.Model.Logics.Payments
{
    /// <summary>
    /// Оплата-ограничитель: списания нет, но покупка разрешается, пока суммарное количество уже выданных
    /// пачек товара плюс запрошенное не превысит <c>maxCount</c> — за всё время либо за окно
    /// <c>timeForCheck</c>. Считает по per-item истории (<c>GetGoodsHistory</c>), учитывая только
    /// доведённые до Delivered покупки.
    /// </summary>
    [Serializable]
    public class LimitedLogic : PaymentLogic
    {
        /// <summary>
        /// Максимальное кол-во вообще или за период (зависит от настройки поля тайминга)
        /// </summary>
        [SerializeField, Min(1)] private int maxCount = 1;

        /// <summary>
        /// За какой период проверять.
        /// Если 0 - то за весь период покупок
        /// </summary>
        [SerializeField, TimeDraw, Min(0)] private long timeForCheck;

        public override int GetCount() => 1;

        public override UniTask<bool> CanPay(string guid, int count, CancellationToken ct)
        {
            var list = ShopOperationsBus.GetGoodsHistory(guid);
            var countGoods = 0;
            var lockCount = maxCount - count;
            if (lockCount < 0)
                return UniTask.FromResult(false);

            if (list == null || list.Count == 0)
                return UniTask.FromResult(true);

            var time = timeForCheck == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timeForCheck;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var operation = list[i];
                if (operation.State != PurchaseState.Delivered)
                    continue;
                var listTransactions = ShopOperationsBus.GetPurchaseHistory(operation.PurchaseGuid);
                if (listTransactions.Count == 0)
                {
                    Debug.LogError(
                        $"[LimitedLogic] Обнаружена операция без событий транзакции! \npurchaseGuid: {operation.PurchaseGuid}");
                    continue;
                }

                var lastTransaction = listTransactions[^1];
                if (lastTransaction.Timestamp < time)
                    break;
                countGoods += operation.RequestedCount;
                if (countGoods > lockCount)
                    return UniTask.FromResult(false);
            }

            return UniTask.FromResult(true);
        }

        public override UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct) =>
            UniTask.FromResult(true);

        public override UniTask<bool> MakeRefund(ShopOperation operation) =>
            UniTask.FromResult(true);
    }
}
#endif