using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.ShopSystem.Model.Logics.Payments
{
    public class LimitedLogic : PaymentLogic
    {
        /// <summary>
        /// Максимальное кол-во вообще или за период (зависит от настройки поля тайминга)
        /// </summary>
        [SerializeField, Min(0)] private int maxCount;

        /// <summary>
        /// За какой период проверять.
        /// Если 0 - то за весь период покупок
        /// </summary>
        [SerializeField, TimeDraw, Min(0)] private long timeForCheck;

        public override int GetCount() => 1;

        public override async UniTask<bool> CanPay(string guid, int count, CancellationToken ct)
        {
            var list = ShopOperationsBus.GetHistory();
            var countGoods = 0;
            var time = timeForCheck == 0 ? 0 : TimeController.Timestamp - timeForCheck;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var purchase = list[i];
                if (purchase.Timestamp < time)
                    break;
                if (purchase.Type != PurchaseState.Delivered)
                    continue;
                if (purchase.ItemGuid == guid)
                    countGoods++;
                if (countGoods >= maxCount)
                    return false;
            }

            return true;
        }

        public override async UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct) => true;

        public override async UniTask<bool> MakeRefund(ShopOperation operation) => true;
    }
}