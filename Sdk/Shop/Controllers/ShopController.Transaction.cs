using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.Shop.Logic;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Controllers
{
    public partial class ShopController
    {
        /// <summary>Покупки, по которым прямо сейчас идёт операция (Inv-8: не более одной за раз).</summary>
        private readonly HashSet<string> _busy = new();

        private enum DeliveryOrigin
        {
            /// <summary>Начальная выдача после Paid — при провале применяется политика фолбэка.</summary>
            Initial,

            /// <summary>Подтверждение из Ready — при провале удерживается в Ready.</summary>
            ConfirmFromReady,

            /// <summary>Повтор из Pending — при провале удерживается в Pending.</summary>
            RetryFromPending
        }

        #region Transaction

        public async UniTask<ShopResult> Buy(string itemGuid, int count)
        {
            if (count <= 0)
                return ShopResult.Refused(ShopRefusal.InvalidCount); // Inv-3

            // Журнал — единый контур записи. Без него оплата прошла бы без следа (нарушение Inv-9),
            // причём молча (Emit — no-op). Отказываем ДО любого списания. Fail-loud: null-журнал в
            // момент покупки = нет активной сессии либо managed-stripping вырезал ShopStatisticsData.
            if (Journal == null)
            {
                Debug.LogError("[Shop] Buy отклонён: журнал недоступен (нет активной игровой сессии / слота).");
                return ShopResult.Refused(ShopRefusal.EngineUnavailable);
            }

            if (HasOrderInFlight())
                return ShopResult.Refused(ShopRefusal.OrderInProgress); // Inv-7

            var item = GetItem(itemGuid);
            if (item.PaymentLogic == null || item.BuyCaseLogic == null)
                return ShopResult.Refused(ShopRefusal.LogicUnavailable); // пустой товар (Inv-5)

            var payRefusal = item.PaymentLogic.CanPay(count);
            if (payRefusal.HasValue)
                return ShopResult.Refused(payRefusal.Value);
            var buyRefusal = item.BuyCaseLogic.CanDeliver(count);
            if (buyRefusal.HasValue)
                return ShopResult.Refused(buyRefusal.Value);

            // отклонённые предпроверкой заказы не журналируются — Ordered пишется только сейчас
            var purchaseGuid = Crypto.GetNewGuid();
            Emit(purchaseGuid, itemGuid, PurchaseState.Ordered, count, 0, 0);

            return await RunPaymentAndDeliver(purchaseGuid, itemGuid, item, count);
        }

        /// <summary>
        /// Пост-Ordered поток: оплата → ветвление → выдача по политике. Общий для начального Buy
        /// и для возобновления зависшего Ordered после перезапуска (ResumeOrder). Покупка на входе
        /// уже в Ordered; заказ здесь не заводится.
        /// </summary>
        private async UniTask<ShopResult> RunPaymentAndDeliver(string purchaseGuid, string itemGuid,
            ShopItemRecord item, int count)
        {
            var payCtx = new PayContext(this, purchaseGuid, itemGuid, count);
            _busy.Add(purchaseGuid);
            // Логики — внешний код L4, могут быть сетевыми и бросать. Исключение НЕ должно оставить
            // покупку в Ordered: иначе HasOrderInFlight заблокирует магазин до конца слота (C1).
            try { await item.PaymentLogic.Pay(payCtx); }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy.Remove(purchaseGuid); }

            var state = StateOf(purchaseGuid);
            if (state == PurchaseState.Cancelled)
                return ShopResult.Completed(Fold(purchaseGuid), payCtx.CancelReason);

            // Логика не доложила исход (молчание или исключение) — безопасно закрываем как Cancelled
            // (подтверждённого списания нет). Причина — LogicRejected, чтобы код не терялся.
            if (state != PurchaseState.Paid)
            {
                Emit(purchaseGuid, itemGuid, PurchaseState.Cancelled, count, 0, 0);
                return ShopResult.Completed(Fold(purchaseGuid), ShopRefusal.LogicRejected);
            }

            // Paid. Политика Ready: автовыдачи нет — ждём подтверждения игроком
            if (Mode == AfterpayMode.Ready)
            {
                Emit(purchaseGuid, itemGuid, PurchaseState.Ready, count, payCtx.PayValue, 0);
                return ShopResult.Completed(Fold(purchaseGuid));
            }

            await RunDelivery(purchaseGuid, itemGuid, item, count, payCtx.PayValue, DeliveryOrigin.Initial);
            return ShopResult.Completed(Fold(purchaseGuid));
        }

        public void BuyForget(string itemGuid, int count) =>
            Buy(itemGuid, count).Forget(Debug.LogException);

        /// <summary>
        /// Возобновление зависшего Ordered после перезапуска: перевызывает PaymentLogic.Pay по тому же
        /// purchaseGuid, чтобы логика реконсилировала статус оплаты (идемпотентность по purchaseGuid —
        /// её ответственность). Симметрично RetryDelivery/ConfirmDelivery. Без этого хука Ordered,
        /// застрявший при обрыве сетевой оплаты, вечно блокировал бы магазин через HasOrderInFlight (Inv-7).
        ///
        /// Точку вызова определяет проект: после загрузки перебрать GetOpen(), для State == Ordered — сюда.
        /// PurchaseBusy при занятости; null если покупка не в Ordered (нечего возобновлять).
        /// </summary>
        public ShopRefusal? ResumeOrder(string purchaseGuid)
        {
            if (Journal == null)
                return ShopRefusal.EngineUnavailable;
            if (string.IsNullOrEmpty(purchaseGuid) || _busy.Contains(purchaseGuid))
                return ShopRefusal.PurchaseBusy; // Inv-8
            if (StateOf(purchaseGuid) != PurchaseState.Ordered)
                return null; // не застрявший Ordered — нечего возобновлять

            ResumePayment(purchaseGuid).Forget(Debug.LogException);
            return null;
        }

        private async UniTask ResumePayment(string purchaseGuid)
        {
            var p = Fold(purchaseGuid);
            var item = GetItem(p.ItemGuid);
            if (item.PaymentLogic == null)
            {
                // логика оплаты исчезла — реконсилировать нечем. Подтверждённого списания в журнале нет,
                // поэтому безопасно закрываем как Cancelled (аварийное разрешение зависшего Ordered).
                Emit(purchaseGuid, p.ItemGuid, PurchaseState.Cancelled, p.RequestedCount, 0, 0);
                return;
            }

            await RunPaymentAndDeliver(purchaseGuid, p.ItemGuid, item, p.RequestedCount);
        }

        /// <summary>
        /// Подтверждение получения из Ready. Синхронно возвращает отказ, если покупка занята
        /// (PurchaseBusy) или не в Ready (null — нечего подтверждать). Сама выдача идёт асинхронно.
        /// </summary>
        public ShopRefusal? ConfirmDelivery(string purchaseGuid) =>
            BeginResolve(purchaseGuid, PurchaseState.Ready, DeliveryOrigin.ConfirmFromReady);

        /// <summary>Повтор выдачи из Pending. Семантика возврата — как у <see cref="ConfirmDelivery"/>.</summary>
        public ShopRefusal? RetryDelivery(string purchaseGuid) =>
            BeginResolve(purchaseGuid, PurchaseState.Pending, DeliveryOrigin.RetryFromPending);

        /// <summary>Ручная отмена открытой покупки с возвратом. PurchaseBusy при занятости, null если нечего отменять.</summary>
        public ShopRefusal? CancelWithRefund(string purchaseGuid)
        {
            if (string.IsNullOrEmpty(purchaseGuid) || _busy.Contains(purchaseGuid))
                return ShopRefusal.PurchaseBusy; // Inv-8

            var p = Fold(purchaseGuid);
            if (!p.Exists || p.IsClosed)
                return null; // нечего отменять

            CancelAsync(purchaseGuid).Forget(Debug.LogException);
            return null;
        }

        private ShopRefusal? BeginResolve(string purchaseGuid, PurchaseState required, DeliveryOrigin origin)
        {
            if (string.IsNullOrEmpty(purchaseGuid) || _busy.Contains(purchaseGuid))
                return ShopRefusal.PurchaseBusy; // Inv-8, теперь сообщается вызывающей стороне

            if (StateOf(purchaseGuid) != required)
                return null; // не в требуемом состоянии — нечего делать (no-op)

            ResolveOpen(purchaseGuid, origin).Forget(Debug.LogException);
            return null;
        }

        #endregion

        #region Delivery / Refund branches

        private async UniTask ResolveOpen(string purchaseGuid, DeliveryOrigin origin)
        {
            var p = Fold(purchaseGuid);
            var item = GetItem(p.ItemGuid);
            if (item.BuyCaseLogic == null)
            {
                // ни выдать, ни вернуть — логик нет (Inv-5): аварийный тупик
                Emit(purchaseGuid, p.ItemGuid, PurchaseState.Failed, p.RequestedCount, p.PayValue, 0);
                return;
            }

            await RunDelivery(purchaseGuid, p.ItemGuid, item, p.RequestedCount, p.PayValue, origin);
        }

        /// <summary>
        /// Единая выдача. Логика докладывает исход через контекст (Delivered/Ready/Pending/Failed).
        /// Исключение логики трактуется как Failed-молчание. При провале/молчании:
        /// Initial → политика фолбэка; ConfirmFromReady → остаётся Ready; RetryFromPending → остаётся Pending.
        /// </summary>
        private async UniTask RunDelivery(string purchaseGuid, string itemGuid,
            ShopItemRecord item, int count, int payValue, DeliveryOrigin origin)
        {
            var buyCtx = new BuyContext(this, purchaseGuid, itemGuid, count, payValue);
            _busy.Add(purchaseGuid);
            // Исключение выдачи НЕ должно оставить оплаченную покупку висеть без фолбэка (I1) —
            // трактуем как провал и идём по общей ветке.
            try { await item.BuyCaseLogic.Deliver(buyCtx); }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy.Remove(purchaseGuid); }

            // Delivered / Ready / Pending уже эмитнуты контекстом
            if (buyCtx.Outcome is BuyOutcome.Delivered or BuyOutcome.Ready or BuyOutcome.Pending)
                return;

            // Failed или молчание/исключение
            if (buyCtx.FailReason.HasValue)
                Debug.LogWarning($"[Shop] Delivery failed for {purchaseGuid}: {buyCtx.FailReason.Value}");

            if (origin != DeliveryOrigin.Initial)
                return; // подтверждение/повтор — состояние (Ready/Pending) сохраняется

            await ApplyFallback(purchaseGuid, itemGuid, item, count, payValue);
        }

        /// <summary>Политика фолбэка при провале начальной выдачи. Ready сюда не попадает — он шунтирован в Buy до RunDelivery.</summary>
        private async UniTask ApplyFallback(string purchaseGuid, string itemGuid,
            ShopItemRecord item, int count, int payValue)
        {
            switch (Mode)
            {
                case AfterpayMode.Rollback:
                    await Rollback(purchaseGuid, itemGuid, item, count, payValue);
                    break;
                case AfterpayMode.Pending:
                    Emit(purchaseGuid, itemGuid, PurchaseState.Pending, count, payValue, 0);
                    break;
            }
        }

        private async UniTask CancelAsync(string purchaseGuid)
        {
            var p = Fold(purchaseGuid);
            if (!p.Exists || p.IsClosed)
                return;

            var item = GetItem(p.ItemGuid);
            await Rollback(purchaseGuid, p.ItemGuid, item, p.RequestedCount, p.PayValue);
        }

        private async UniTask Rollback(string purchaseGuid, string itemGuid,
            ShopItemRecord item, int count, int payValue)
        {
            if (item.PaymentLogic == null)
            {
                // вернуть нечем — аварийный тупик (Inv-5)
                Emit(purchaseGuid, itemGuid, PurchaseState.Failed, count, payValue, 0);
                return;
            }

            var refundCtx = new RefundContext(this, purchaseGuid, itemGuid, count, payValue);
            _busy.Add(purchaseGuid);
            // Исключение возврата = провал возврата: покупка остаётся открытой (Inv-6), терминал не пишем.
            try { await item.PaymentLogic.Refund(refundCtx); }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy.Remove(purchaseGuid); }

            if (!refundCtx.Succeeded && refundCtx.FailReason.HasValue)
                Debug.LogWarning($"[Shop] Refund failed for {purchaseGuid}: {refundCtx.FailReason.Value}");
        }

        #endregion

        #region Invariants

        private bool HasOrderInFlight()
        {
            EnsureIndex();
            foreach (var guid in _order)
                if (_index.TryGetValue(guid, out var fs) && fs.Purchase.State == PurchaseState.Ordered)
                    return true;
            return false;
        }

        #endregion

        #region Scoped contexts

        private enum BuyOutcome { None, Delivered, Ready, Pending, Failed }

        private sealed class PayContext : IPayContext
        {
            private readonly ShopController _owner;
            private readonly string _purchaseGuid;
            private readonly string _itemGuid;
            private bool _resolved;

            public PayContext(ShopController owner, string purchaseGuid, string itemGuid, int count)
            {
                _owner = owner;
                _purchaseGuid = purchaseGuid;
                _itemGuid = itemGuid;
                RequestedCount = count;
            }

            public int RequestedCount { get; }
            public int PayValue { get; private set; }

            /// <summary>Причина отмены оплаты — прокидывается наружу в ShopResult.</summary>
            public ShopRefusal? CancelReason { get; private set; }

            public void Paid(int payValue)
            {
                if (_resolved) return;
                _resolved = true;
                PayValue = payValue;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Paid, RequestedCount, payValue, 0);
            }

            public void Cancelled(ShopRefusal reason)
            {
                if (_resolved) return;
                _resolved = true;
                CancelReason = reason;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Cancelled, RequestedCount, 0, 0);
            }
        }

        private sealed class BuyContext : IBuyContext
        {
            private readonly ShopController _owner;
            private readonly string _purchaseGuid;
            private readonly string _itemGuid;

            public BuyContext(ShopController owner, string purchaseGuid, string itemGuid, int count, int payValue)
            {
                _owner = owner;
                _purchaseGuid = purchaseGuid;
                _itemGuid = itemGuid;
                RequestedCount = count;
                PayValue = payValue;
            }

            public int RequestedCount { get; }
            public int PayValue { get; }
            public BuyOutcome Outcome { get; private set; } = BuyOutcome.None;

            /// <summary>Причина провала выдачи — логируется контроллером перед применением политики.</summary>
            public ShopRefusal? FailReason { get; private set; }

            public void Delivered(int buyValue)
            {
                if (Outcome != BuyOutcome.None) return;
                Outcome = BuyOutcome.Delivered;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Delivered, RequestedCount, PayValue, buyValue);
            }

            public void Ready()
            {
                if (Outcome != BuyOutcome.None) return;
                Outcome = BuyOutcome.Ready;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Ready, RequestedCount, PayValue, 0);
            }

            public void Pending()
            {
                if (Outcome != BuyOutcome.None) return;
                Outcome = BuyOutcome.Pending;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Pending, RequestedCount, PayValue, 0);
            }

            public void Failed(ShopRefusal reason)
            {
                if (Outcome != BuyOutcome.None) return;
                Outcome = BuyOutcome.Failed; // терминал не пишем — политику применит контроллер
                FailReason = reason;
            }
        }

        private sealed class RefundContext : IRefundContext
        {
            private readonly ShopController _owner;
            private readonly string _purchaseGuid;
            private readonly string _itemGuid;
            private readonly int _count;
            private bool _resolved;

            public RefundContext(ShopController owner, string purchaseGuid, string itemGuid, int count, int payValue)
            {
                _owner = owner;
                _purchaseGuid = purchaseGuid;
                _itemGuid = itemGuid;
                _count = count;
                PayValue = payValue;
            }

            public int PayValue { get; }
            public bool Succeeded { get; private set; }

            /// <summary>Причина провала возврата — логируется контроллером.</summary>
            public ShopRefusal? FailReason { get; private set; }

            public void Refunded()
            {
                if (_resolved) return;
                _resolved = true;
                Succeeded = true;
                _owner.Emit(_purchaseGuid, _itemGuid, PurchaseState.Refunded, _count, PayValue, 0);
            }

            public void Failed(ShopRefusal reason)
            {
                if (_resolved) return;
                _resolved = true; // терминал не пишем — покупка остаётся открытой (Inv-6)
                FailReason = reason;
            }
        }

        #endregion
    }
}
