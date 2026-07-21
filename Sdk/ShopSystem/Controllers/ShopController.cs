#if USING_VORTEX_SHOP
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.Controllers
{
    public class ShopController : IShopController
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap() => ShopBus.SetDriver(new ShopController());

        public event Action OnInit;

        private Dictionary<string, ShopItemModel> _shopItems;

        // Общий токен отмены: один активный интерактивный процесс за раз (Buy/Confirm/Retry).
        // Восстановление после загрузки идёт на локальном токене и под эту отмену не попадает.
        private CancellationTokenSource _cts;
        private UniTask _running;
        private string _activeGuid;

        /// <summary>
        /// Идёт ли сейчас интерактивный процесс. Форсирует модель «один активный процесс»:
        /// новый Buy/Confirm/Retry отбивается, пока предыдущий не завершился (защита от двойного вызова).
        /// Публичен для внешней пред-проверки — вызывающий сам разделяет реакцию на «занято».
        /// </summary>
        public bool IsBusy => _activeGuid != null;

        public void Init()
        {
            var items = Database.GetRecords<ShopItemModel>();
            foreach (var item in items)
                _shopItems.Add(item.GuidPreset, item);

            OnInit?.Invoke();
        }

        public void Destroy()
        {
        }

        public async UniTask<ShopOperation> Buy(string itemGuid, int count)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[ShopController] Busy: покупка отклонена, идёт другой процесс");
                return null;
            }

            var item = GetItem(itemGuid);
            if (item == null) return null;

            var operation = MakeNewOperation(item, count);
            await Launch(operation, ct => RunProcess(item, operation, ct));
            return operation;
        }

        public ShopOperation BuyForget(string itemGuid, int count)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[ShopController] Busy: покупка отклонена, идёт другой процесс");
                return null;
            }

            var item = GetItem(itemGuid);
            if (item == null) return null;

            var operation = MakeNewOperation(item, count);
            Launch(operation, ct => RunProcess(item, operation, ct)).Forget(Debug.LogException);

            return operation;
        }

        /// <summary>
        /// Прогон открытой покупки до терминала/равновесия. Точка входа восстановления после загрузки.
        /// Работает на локальном токене — на этапе восстановления внешняя отмена не подключается.
        /// </summary>
        public async UniTask Processing(ShopOperation operation, ShopItemModel item = null)
        {
            using var cts = new CancellationTokenSource();
            try
            {
                await ProcessLoop(operation, cts.Token, item);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async UniTask ConfirmDelivery(ShopOperation operation, ShopItemModel item = null)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[ShopController] Busy: подтверждение отклонено, идёт другой процесс");
                return;
            }

            if (operation.State.Value != PurchaseState.Ready)
            {
                Debug.LogError("[ShopController] Wrong state of operation for ConfirmDelivery");
                return;
            }

            await Launch(operation, ct => MakeDelivery(operation, ct, item));
        }

        public async UniTask RetryDelivery(ShopOperation operation, ShopItemModel item = null)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[ShopController] Busy: повтор выдачи отклонён, идёт другой процесс");
                return;
            }

            if (operation.State.Value != PurchaseState.Pending)
            {
                Debug.LogError("[ShopController] Wrong state of operation for RetryDelivery");
                return;
            }

            await Launch(operation, ct => MakeDelivery(operation, ct, item));
        }

        /// <summary>
        /// Внешняя отмена покупки. Если по этой покупке идёт процесс — прерывает его и ждёт размотки
        /// до чистой границы состояния, затем по финальному состоянию: оплата проведена
        /// (Paid/Ready/Pending) — возврат через <see cref="Refund"/>; ещё не оплачено (Ordered) —
        /// закрытие как Cancelled; терминальное — ничего.
        /// </summary>
        public async UniTask CancelWithRefund(ShopOperation operation)
        {
            if (_cts != null && _activeGuid == operation.PurchaseGuid)
            {
                _cts.Cancel();
                try
                {
                    await _running;
                }
                catch (OperationCanceledException)
                {
                }
            }

            switch (operation.State.Value)
            {
                case PurchaseState.Paid:
                case PurchaseState.Ready:
                case PurchaseState.Pending:
                    await Refund(operation);
                    break;
                case PurchaseState.Ordered:
                    operation.SetState(PurchaseState.Cancelled);
                    ShopOperationsBus.MakeRecord(operation);
                    break;
                default:
                    // Delivered / Refunded / Cancelled / Failed / NotStarted — отменять нечего
                    break;
            }
        }

        public void SetIndex(Dictionary<string, ShopItemModel> shopItems)
        {
            _shopItems = shopItems;
        }

        /// <summary>
        /// Обёртка запуска интерактивного процесса: заводит общий токен отмены и трек активной покупки,
        /// чтобы <see cref="CancelWithRefund"/> мог прервать именно её. Отмена гасится тихо — финальный
        /// статус доводит вызвавший CancelWithRefund.
        /// </summary>
        private async UniTask Launch(ShopOperation operation, Func<CancellationToken, UniTask> process)
        {
            var cts = new CancellationTokenSource();
            _cts = cts;
            _activeGuid = operation.PurchaseGuid;
            _running = process(cts.Token).Preserve();

            try
            {
                await _running;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_activeGuid == operation.PurchaseGuid) _activeGuid = null;
                if (ReferenceEquals(_cts, cts)) _cts = null;
                cts.Dispose();
            }
        }

        private async UniTask RunProcess(ShopItemModel item, ShopOperation operation, CancellationToken ct)
        {
            try
            {
                //Первичная проверка возможности покупки
                if (!await item.PaymentLogic.CanPay(operation.RequestedCount, ct))
                {
                    operation.SetState(PurchaseState.NotStarted);
                    return;
                }

                if (!await item.DeliveryLogic.CanDelivery(operation.RequestedCount, ct))
                {
                    operation.SetState(PurchaseState.NotStarted);
                    return;
                }

                //Фиксируем факт заказа, запускаем процесс
                ShopOperationsBus.MakeRecord(operation);
                await ProcessLoop(operation, ct, item);
            }
            catch (OperationCanceledException)
            {
                //Прервано внешней отменой — финальный статус доводит CancelWithRefund
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Цикл продвижения покупки. Останавливается на терминальном состоянии, равновесии (Ready/Pending)
        /// или отмене токена. Фактически циклятся только Ordered и Paid.
        /// </summary>
        private async UniTask ProcessLoop(ShopOperation operation, CancellationToken ct, ShopItemModel item = null)
        {
            while (operation.State is not
                   {
                       Value: PurchaseState.NotStarted
                       or PurchaseState.Cancelled
                       or PurchaseState.Failed
                       or PurchaseState.Delivered
                       or PurchaseState.Refunded
                   })
            {
                //Ready — состояние ожидания игрока: автопрогон (восстановление) его не двигает.
                //Выдачу из Ready инициирует только явный ConfirmDelivery (вне цикла).
                if (operation.State.Value == PurchaseState.Ready)
                    break;

                ct.ThrowIfCancellationRequested();
                await NextStep(operation, ct, item);

                //Прерывание для равновесных состояний (одна попытка до фиксации)
                if (operation.State is
                    {
                        Value: PurchaseState.Ready
                        or PurchaseState.Pending
                    }) break;
            }
        }

        private async UniTask NextStep(ShopOperation operation, CancellationToken ct, ShopItemModel item = null)
        {
            item ??= GetItem(operation.ItemGuid);
            if (item == null) return;

            switch (operation.State.Value)
            {
                case PurchaseState.Ordered:
                    await MakePayment(item, operation, ct);
                    break;
                case PurchaseState.Paid:
                case PurchaseState.Ready:
                case PurchaseState.Pending:
                    await MakeDelivery(operation, ct, item);
                    break;
                case PurchaseState.Delivered:
                case PurchaseState.Refunded:
                case PurchaseState.NotStarted:
                case PurchaseState.Cancelled:
                case PurchaseState.Failed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async UniTask MakePayment(ShopItemModel item, ShopOperation operation, CancellationToken ct)
        {
            if (!await item.PaymentLogic.MakePay(operation, ct))
            {
                operation.SetState(PurchaseState.Cancelled);
                ShopOperationsBus.MakeRecord(operation);
                return;
            }

            operation.SetState(PurchaseState.Paid);
            ShopOperationsBus.MakeRecord(operation);
        }

        private async UniTask MakeDelivery(ShopOperation operation, CancellationToken ct, ShopItemModel item = null)
        {
            try
            {
                var state = operation.State.Value;
                if (state is not (PurchaseState.Ready or PurchaseState.Pending or PurchaseState.Paid))
                {
                    Debug.LogError("[ShopController] Wrong state of operation for Deliver");
                    return;
                }

                if (item == null && !_shopItems.TryGetValue(operation.ItemGuid, out item))
                {
                    Debug.LogError($"[ShopController] Shop Item {operation.ItemGuid} not found");
                    return;
                }

                var settings = ShopBus.Settings;
                if (!await item.DeliveryLogic.CanDelivery(operation.RequestedCount, ct)
                    || settings.AfterpayMode == AfterpayMode.Ready)
                {
                    switch (settings.AfterpayMode)
                    {
                        case AfterpayMode.Pending:
                            if (operation.State.Value == PurchaseState.Paid)
                            {
                                operation.SetState(PurchaseState.Pending);
                                ShopOperationsBus.MakeRecord(operation);
                            }

                            break;
                        case AfterpayMode.Rollback:
                            await Refund(operation, item);
                            break;
                        case AfterpayMode.Ready:
                            if (operation.State.Value == PurchaseState.Paid)
                            {
                                operation.SetState(PurchaseState.Ready);
                                ShopOperationsBus.MakeRecord(operation);
                            }

                            return;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return;
                }

                if (!await item.DeliveryLogic.MakeDelivery(operation, ct))
                    return;

                operation.SetState(PurchaseState.Delivered);
                ShopOperationsBus.MakeRecord(operation);
            }
            catch (OperationCanceledException)
            {
                //Отмена должна размотаться наверх (в цикл / Launch), а не гаситься здесь
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Возврат оплаты. Компенсирующее действие — не завязано на токен отмены, обязано отработать
        /// до конца. При успехе переводит покупку в Refunded. Вызывается из Rollback-ветки выдачи
        /// и из <see cref="CancelWithRefund"/>.
        /// </summary>
        private async UniTask Refund(ShopOperation operation, ShopItemModel item = null)
        {
            item ??= GetItem(operation.ItemGuid);
            if (item == null) return;

            if (await item.PaymentLogic.MakeRefund(operation))
            {
                operation.SetState(PurchaseState.Refunded);
                ShopOperationsBus.MakeRecord(operation);
            }
        }

        private ShopOperation MakeNewOperation(ShopItemModel item, int count)
        {
            var operation = new ShopOperation()
            {
                PurchaseGuid = Crypto.GetNewGuid(),
                ItemGuid = item.GuidPreset,
                BuyValue = item.DeliveryLogic.GetCount(),
                PayValue = item.PaymentLogic.GetCount(),
                RequestedCount = count
            };
            return operation;
        }

        private ShopItemModel GetItem(string guid)
        {
            if (_shopItems.TryGetValue(guid, out var item)) return item;
            Debug.LogError($"[ShopController] Shop Item {guid} not found");
            return null;
        }
    }
}
#endif