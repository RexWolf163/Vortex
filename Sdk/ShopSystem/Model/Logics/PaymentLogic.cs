#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    /// <summary>
    /// Абстрактная логика оплаты товара — сторона списания/возврата в сделке. Полиморфна (задаётся
    /// через [SerializeReference] в пресете товара). Обязательна для каждого товара; бесплатный товар —
    /// отдельная реализация с нулевой ценой.
    /// </summary>
    [Serializable]
    public abstract class PaymentLogic
    {
        [DisplayAsString, ShowInInspector, HideLabel, PropertyOrder(-100)]
        private string Label => GetType().Name;

        /// <summary>
        /// Возвращает опорное численное значение
        /// </summary>
        /// <returns></returns>
        public abstract int GetCount();

        /// <summary>
        /// GUID предмета, к которому привязана логика (валюта оплаты). Доступен для чтения UI.
        /// null — логика не предметная (напр. бесплатно/лимит).
        /// </summary>
        public virtual string GetItemGuid() => null;

        /// <summary>
        /// Проверка возможности оплаты заданного кол-ва пакетов
        /// </summary>
        /// <param name="guid">GUID товара</param>
        /// <param name="count"></param>
        /// <param name="ct">Токен отмены. Отмена прерывает проверку.</param>
        /// <returns></returns>
        public abstract UniTask<bool> CanPay(string guid, int count, CancellationToken ct);

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
        /// Возврат false закрывает покупку как Failed («ни выдать, ни вернуть») — она уйдёт
        /// в GetStuck на ручной разбор.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public abstract UniTask<bool> MakeRefund(ShopOperation operation);
    }
}
#endif