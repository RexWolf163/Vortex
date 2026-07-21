#if USING_VORTEX_SHOP
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem
{
    /// <summary>Контракт писателя журнала магазина: фиксация текущего состояния операции событием.</summary>
    public interface IShopTransactionsController
    {
        /// <summary>Создать новую запись журнала о текущем состоянии операции. Возвращает успех записи.</summary>
        /// <param name="operation">Операция, чьё состояние фиксируется.</param>
        public bool MakeRecord(ShopOperation operation);
    }
}
#endif