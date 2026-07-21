#if USING_VORTEX_SHOP
using System;
using System.Threading;
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
        /// <param name="ct">Токен отмены. Отмена прерывает проверку.</param>
        /// <returns></returns>
        public abstract UniTask<bool> CanDelivery(int count, CancellationToken ct);

        /// <summary>
        /// Попытка получения по указанной операции.
        /// При отмене должна пробросить <see cref="OperationCanceledException"/>.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="ct">Токен отмены идущего процесса.</param>
        /// <returns></returns>
        public abstract UniTask<bool> MakeDelivery(ShopOperation operation, CancellationToken ct);
    }
}
#endif