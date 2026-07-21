#if USING_VORTEX_SHOP
using System;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.Controllers
{
    public class ShopTransactionsController : Singleton<ShopTransactionsController>, IShopTransactionsController
    {
        public bool MakeRecord(ShopOperation operation)
        {
            try
            {
                var data = ShopOperationsBus.Data;
                var number = (data.Events.Count == 0) ? 0 : data.Events.GetList()[^1].Sequence + 1;

                data.Events.Add(new ShopTransactionEvent()
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
                });

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