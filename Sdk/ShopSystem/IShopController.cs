#if USING_VORTEX_SHOP
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem
{
    public interface IShopController : ISystemDriver
    {
        /// <summary>Покупка. Возвращает Модель операции.</summary>
        public UniTask<ShopOperation> Buy(string itemGuid, int count);

        /// <summary>Синхронный fire-and-forget враппер над <see cref="Buy"/> (Forget + лог исключений).</summary>
        public ShopOperation BuyForget(string itemGuid, int count);

        /// <summary>
        /// Подтверждение получения из Ready
        /// </summary>
        public UniTask ConfirmDelivery(ShopOperation operation, ShopItemModel item = null);

        /// <summary>
        /// Повтор выдачи из Pending
        /// </summary>
        public UniTask RetryDelivery(ShopOperation operation, ShopItemModel item = null);

        /// <summary>
        /// Запуск следующего этапа транзакции
        /// </summary>
        public UniTask Processing(ShopOperation operation, ShopItemModel item = null);

        /// <summary>
        /// Ручная отмена открытой покупки с возвратом оплаты.
        /// </summary>
        public UniTask CancelWithRefund(ShopOperation operation, ShopItemModel item = null);

        /// <summary>
        /// Передача индекса под заполнение
        /// </summary>
        /// <param name="shopItems"></param>
        void SetIndex(Dictionary<string, ShopItemModel> shopItems);
    }
}
#endif