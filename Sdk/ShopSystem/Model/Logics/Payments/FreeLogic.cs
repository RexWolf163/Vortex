#if USING_VORTEX_SHOP
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.ShopSystem.Model.Logics.Payments
{
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