#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    /// <summary>
    /// Абстрактная логика выдачи товара — сторона получения в сделке. Полиморфна (задаётся через
    /// [SerializeReference] в пресете товара). Обязательна для каждого товара.
    /// </summary>
    [Serializable]
    public abstract class DeliveryLogic
    {
        [DisplayAsString, ShowInInspector, HideLabel, PropertyOrder(-100)]
        private string Label => GetType().Name;

        /// <summary>
        /// Возвращает опорное численное значение
        /// </summary>
        /// <returns></returns>
        public abstract int GetCount();

        /// <summary>
        /// Проверка возможности получения заданного кол-ва пакетов
        /// </summary>
        /// <param name="guid">GUID товара</param>
        /// <param name="count"></param>
        /// <param name="ct">Токен отмены. Отмена прерывает проверку.</param>
        /// <returns></returns>
        public abstract UniTask<bool> CanDelivery(string guid, int count, CancellationToken ct);

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