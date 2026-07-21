#if USING_VORTEX_SHOP
using System;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.Controllers
{
    /// <summary>
    /// Писатель журнала: собирает событие из текущего состояния операции (со сквозным Sequence и осями
    /// времени) и добавляет его в журнал и индексы через owner-ключ модели (<see cref="ShopOperationsController"/>).
    /// Stateless — вся работа над данными сессии из <see cref="ShopOperationsBus.Data"/>.
    /// </summary>
    public class ShopTransactionsController : Singleton<ShopTransactionsController>, IShopTransactionsController
    {
        /// <summary>
        /// Собрать событие по текущему состоянию операции и записать в журнал/индексы. Возвращает false
        /// при исключении (логируется). Не вызывать вне загруженной сессии — журнала ещё нет.
        /// </summary>
        public bool MakeRecord(ShopOperation operation)
        {
            try
            {
                var data = ShopOperationsBus.Data;
                var number = (data.Events.Count == 0) ? 0 : data.Events.GetList()[^1].Sequence + 1;

                var newEvent = new ShopTransactionEvent()
                {
                    PurchaseGuid = operation.PurchaseGuid,
                    ItemGuid = operation.ItemGuid,
                    Type = operation.State,

                    Sequence = number,
                    RequestedCount = operation.RequestedCount,
                    PayValue = operation.PayValue,
                    BuyValue = operation.BuyValue,

                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    PlaySeconds = (long)GameController.PlayTime.TotalSeconds,
                    AppSeconds = (long)GameController.AppTime.TotalSeconds
                };

                newEvent.RegistrationEvent(operation);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
#endif