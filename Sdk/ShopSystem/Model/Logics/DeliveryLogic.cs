using System;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    [Serializable]
    public abstract class DeliveryLogic
    {
        /// <summary>
        /// Возвращает опорное численное значение
        /// </summary>
        /// <returns></returns>
        public abstract int GetCount();

        /// <summary>
        /// Проверка возможности получения заданного кол-ва пакетов
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract UniTask<bool> CanDelivery(int count);

        /// <summary>
        /// Попытка получения по указанной операции
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public abstract UniTask<bool> MakeDelivery(ShopOperation operation);
    }
}