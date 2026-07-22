using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.ShopSystem.Model.Logics.Payments
{
    public class FreeLogic : PaymentLogic
    {
        public override int GetCount() => 1;

        public override async UniTask<bool> CanPay(string guid, int count, CancellationToken ct) => true;

        public override async UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct) => true;

        public override async UniTask<bool> MakeRefund(ShopOperation operation) => true;
    }
}