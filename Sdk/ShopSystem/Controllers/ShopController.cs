#if USING_VORTEX_SHOP
using System;
using System.Collections.Generic;
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
            var item = GetItem(itemGuid);
            if (item == null) return null;

            var operation = MakeNewOperation(item, count);
            await RunProcess(item, operation);
            return operation;
        }

        public ShopOperation BuyForget(string itemGuid, int count)
        {
            var item = GetItem(itemGuid);
            if (item == null) return null;

            var operation = MakeNewOperation(item, count);
            RunProcess(item, operation).Forget(Debug.LogException);

            return operation;
        }

        private async UniTask<ShopOperation> RunProcess(ShopItemModel item, ShopOperation operation)
        {
            try
            {
                //TODO вставить CancelationToken 

                //Первичная проверка возможности покупки
                var b = await item.PaymentLogic.CanPay(operation.RequestedCount);
                if (!b)
                {
                    operation.SetState(PurchaseState.NotStarted);
                    return operation;
                }

                b = await item.DeliveryLogic.CanDelivery(operation.RequestedCount);
                if (!b)
                {
                    operation.SetState(PurchaseState.NotStarted);
                    return operation;
                }

                //Фиксируем факт заказа, запускаем процесс
                ShopOperationsBus.MakeRecord(operation);
                await Processing(operation);

                return operation;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return operation;
            }
        }

        public async UniTask Processing(ShopOperation operation, ShopItemModel item = null)
        {
            while (operation.State is not
                   {
                       Value: PurchaseState.NotStarted
                       or PurchaseState.Cancelled
                       or PurchaseState.Failed
                       or PurchaseState.Delivered
                   })
            {
                await NextStep(operation);
                //Идет бесконечная рекурсия процесса до появления терминального или равновесного состояния
                //Терминальные проверяются внешним условием. Тут прерывание для равновесных (попытка выполнения один раз до фиксации)
                // Фактически циклятся только Ordered и Paid
                if (operation.State is
                    {
                        Value: PurchaseState.Ready
                        or PurchaseState.Pending
                    }) break;
            }
        }

        private async UniTask<ShopOperation> MakeDelivery(ShopOperation operation, ShopItemModel item = null)
        {
            try
            {
                var state = operation.State.Value;
                if (state is not (PurchaseState.Ready or PurchaseState.Pending or PurchaseState.Paid))
                {
                    Debug.LogError("[ShopController] Wrong state of operation for Deliver");
                    return operation;
                }

                if (item == null && !_shopItems.TryGetValue(operation.ItemGuid, out item))
                {
                    Debug.LogError($"[ShopController] Shop Item {operation.ItemGuid} not found");
                    return null;
                }

                var settings = ShopBus.Settings;
                var b = await item.DeliveryLogic.CanDelivery(operation.RequestedCount);
                if (!b)
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
                            await CancelWithRefund(operation);
                            break;
                        case AfterpayMode.Ready:
                            if (operation.State.Value == PurchaseState.Paid)
                            {
                                operation.SetState(PurchaseState.Ready);
                                ShopOperationsBus.MakeRecord(operation);
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return operation;
                }

                var result = await item.DeliveryLogic.MakeDelivery(operation);
                if (!result)
                    return operation;

                operation.SetState(PurchaseState.Delivered);
                ShopOperationsBus.MakeRecord(operation);

                return operation;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return operation;
            }
        }

        public async UniTask ConfirmDelivery(ShopOperation operation, ShopItemModel item = null)
        {
            if (operation.State.Value != PurchaseState.Ready)
            {
                Debug.LogError("[ShopController] Wrong state of operation for ConfirmDelivery");
                return;
            }

            await MakeDelivery(operation, item);
        }

        public async UniTask RetryDelivery(ShopOperation operation, ShopItemModel item = null)
        {
            if (operation.State.Value != PurchaseState.Pending)
            {
                Debug.LogError("[ShopController] Wrong state of operation for RetryDelivery");
                return;
            }

            await MakeDelivery(operation, item);
        }

        private async UniTask NextStep(ShopOperation operation, ShopItemModel item = null)
        {
            item ??= GetItem(operation.ItemGuid);
            if (item == null) return;

            switch (operation.State.Value)
            {
                case PurchaseState.Ordered:
                    await MakePayment(item, operation);
                    break;

                case PurchaseState.Paid:
                    await MakeDelivery(operation, item);
                    break;
                case PurchaseState.Ready:
                    await ConfirmDelivery(operation, item);
                    break;
                case PurchaseState.Pending:
                    await RetryDelivery(operation, item);
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

        public async UniTask CancelWithRefund(ShopOperation operation, ShopItemModel item = null)
        {
            item ??= GetItem(operation.ItemGuid);
            if (item == null) return;

            if (await item.PaymentLogic.MakeRefund(operation))
            {
                operation.SetState(PurchaseState.Refunded);
                ShopOperationsBus.MakeRecord(operation);
            }
        }

        public void SetIndex(Dictionary<string, ShopItemModel> shopItems)
        {
            _shopItems = shopItems;
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

        private async UniTask MakePayment(ShopItemModel item, ShopOperation operation)
        {
            if (!await item.PaymentLogic.MakePay(operation))
            {
                operation.SetState(PurchaseState.Cancelled);
                ShopOperationsBus.MakeRecord(operation);
                return;
            }

            operation.SetState(PurchaseState.Paid);
            ShopOperationsBus.MakeRecord(operation);
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