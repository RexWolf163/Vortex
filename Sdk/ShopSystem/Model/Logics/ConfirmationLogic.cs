#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    /// <summary>
    /// Опциональная логика подтверждения намерения покупки — гейт ПЕРЕД оплатой. Полиморфна
    /// (задаётся через [SerializeReference] в пресете товара). В отличие от <see cref="PaymentLogic"/> и
    /// <see cref="DeliveryLogic"/> необязательна: <c>null</c> на товаре = подтверждение не требуется, покупка
    /// идёт сразу.
    ///
    /// Запускается ровно один раз на реальную покупку — в пути <c>Buy</c>, после <c>CanPay</c>/<c>CanDelivery</c>
    /// и до фиксации заказа. НЕ вызывается спекулятивно при формировании витрины (в отличие от <c>Can*</c>),
    /// поэтому сюда можно класть тяжёлую/сетевую проверку без риска спама. Примеры: UI-диалог подтверждения,
    /// ожидание сигнала «второго клика», серверный ack.
    ///
    /// Возврат <c>false</c> → покупка не начинается (<see cref="PurchaseState.NotStarted"/>, в журнал не
    /// пишется). На этом этапе <c>PurchaseGuid</c> ещё не зафиксирован в журнале: подтверждается НАМЕРЕНИЕ,
    /// а не факт списания — сверку списания держит <see cref="PaymentLogic.MakePay"/>.
    /// </summary>
    [Serializable]
    public abstract class ConfirmationLogic
    {
        [DisplayAsString, ShowInInspector, HideLabel, PropertyOrder(-100)]
        private string Label => GetType().Name;

        /// <summary>
        /// Подтвердить намерение покупки. <c>true</c> → продолжить к оплате; <c>false</c> → отменить
        /// (покупка не начинается). При отмене процесса должна пробросить
        /// <see cref="OperationCanceledException"/>.
        /// </summary>
        /// <param name="guid">GUID товара.</param>
        /// <param name="count">Число запрошенных пачек.</param>
        /// <param name="ct">Токен отмены идущего процесса (например, CancelWithRefund/teardown).</param>
        public abstract UniTask<bool> Confirm(string guid, int count, CancellationToken ct);
    }
}
#endif
