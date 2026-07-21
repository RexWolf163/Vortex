using System;
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
        /// <returns></returns>
        public abstract UniTask<bool> CanPay(int count);

        /// <summary>
        /// Попытка оплаты по торговой операции
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public abstract UniTask<bool> MakePay(ShopOperation operation);

        /// <summary>
        /// Попытка рефанда. При неудаче операция виснет в Paid статусе
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public abstract UniTask<bool> MakeRefund(ShopOperation operation);
    }
}