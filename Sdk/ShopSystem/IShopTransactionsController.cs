using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem
{
    public interface IShopTransactionsController
    {
        /// <summary>
        /// Создать новую запись о текущем состоянии операции покупки
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public bool MakeRecord(ShopOperation operation);
    }
}