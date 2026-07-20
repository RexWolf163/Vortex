using System;
using Cysharp.Threading.Tasks;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Logic
{
    /// <summary>
    /// Логика оплаты товара. Настраивается на пресете через [SerializeReference]; движок не знает,
    /// что и откуда списывается. Сколько платить за единицу — параметр конкретной реализации, не пресета.
    ///
    /// Требования (контракт):
    /// 1. Stateless. CopyFrom прогоняет значения через DeepCopy — запись получает собственный клон;
    ///    GetDataForSave()=>null означает, что состояние логики не сохраняется. Всё для повтора — в журнале.
    /// 2. Предпроверка отсекающая, не гарантирующая: авторитетен результат выполнения.
    /// 3. Единый сохраняемый контур (Inv-9): данные, которые логика изменяет, обязаны сохраняться
    ///    вместе с журналом. Для логики с эффектом в контуре сейва путь Ordered→Paid/Cancelled
    ///    обязан быть синхронным. Асинхронный путь допустим только для эффекта ВНЕ контура сейва
    ///    (серверно-авторитетная оплата); её восстановление после обрыва — ответственность реализации,
    ///    движок гарантирует лишь стабильный purchaseGuid как ключ идемпотентности.
    /// </summary>
    [Serializable]
    public abstract class PaymentLogic
    {
        /// <summary>
        /// Синхронная предпроверка возможности оплаты запрошенного количества.
        /// null — можно; иначе код отказа (обычно InsufficientResources).
        /// </summary>
        public abstract ShopRefusal? CanPay(int requestedCount);

        /// <summary>
        /// Провести оплату. Исход докладывается через <paramref name="context"/>
        /// (Paid либо Cancelled). См. требование 3 про синхронность/асинхронность.
        /// </summary>
        public abstract UniTask Pay(IPayContext context);

        /// <summary>
        /// Вернуть оплату. Исход — через <paramref name="context"/> (Refunded либо Failed).
        /// Упавший возврат оставляет покупку открытой (Inv-6).
        /// </summary>
        public abstract UniTask Refund(IRefundContext context);
    }
}
