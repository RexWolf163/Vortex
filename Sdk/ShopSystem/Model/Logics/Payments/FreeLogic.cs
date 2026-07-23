#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.ShopSystem.Model.Logics.Payments
{
    /// <summary>
    /// Оплата с нулевой ценой: товар отдаётся без списания, возврат всегда успешен. Каноничный способ
    /// собрать бесплатный товар — именно отдельной логикой, а не null-ссылкой: пустая логика делает
    /// товар нефункциональным (покупка уходит в Failed).
    /// </summary>
    [Serializable]
    public class FreeLogic : PaymentLogic
    {
        public override int GetCount() => 1;

        public override UniTask<bool> CanPay(string guid, int count, CancellationToken ct) =>
            UniTask.FromResult(true);

        public override UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct) =>
            UniTask.FromResult(true);

        public override UniTask<bool> MakeRefund(ShopOperation operation) =>
            UniTask.FromResult(true);
    }
}
#endif