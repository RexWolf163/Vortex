#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    [Serializable]
    public abstract class PaymentLogic
    {
        /// <summary>
        /// Возвращает опорное численное значение
        /// </summary>
        /// <returns></returns>
        public abstract int GetCount();

        /// <summary>
        /// Проверка возможности оплаты заданного кол-ва пакетов
        /// </summary>
        /// <param name="count"></param>
        /// <param name="ct">Токен отмены. Отмена прерывает проверку.</param>
        /// <returns></returns>
        public abstract UniTask<bool> CanPay(int count, CancellationToken ct);

        /// <summary>
        /// Попытка оплаты по торговой операции.
        /// При отмене должна пробросить <see cref="OperationCanceledException"/> (для сетевой логики —
        /// сверить факт списания на L4 по PurchaseGuid).
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="ct">Токен отмены идущего процесса.</param>
        /// <returns></returns>
        public abstract UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct);

        /// <summary>
        /// Попытка рефанда. Компенсирующее действие — токена отмены нет, обязано доиграть до конца.
        /// При неудаче операция виснет в Paid статусе.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public abstract UniTask<bool> MakeRefund(ShopOperation operation);
    }
}
#endif