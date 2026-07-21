#if USING_VORTEX_SHOP
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Sdk.ShopSystem.Model.Logics;

namespace Vortex.Sdk.ShopSystem.Model
{
    public class ShopItemModel : Record
    {
        /// <summary>Логика оплаты. null у пустого товара / товара-компенсации без оплаты.</summary>
        public PaymentLogic PaymentLogic { get; protected set; }

        /// <summary>Логика выдачи. null - признак ошибки.</summary>
        public DeliveryLogic DeliveryLogic { get; protected set; }

        /// <summary>
        /// Скрыть в витрине. Статический флаг дизайнера: товары-компенсации и «удалённые» товары
        /// не должны показываться в обычной витрине.
        /// </summary>
        public bool HiddenInShowcase { get; protected set; }

        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
            //Ignore
            //Каталог неизменяем — восстанавливать нечего
        }
    }
}
#endif