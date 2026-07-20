using Vortex.Core.DatabaseSystem.Model;
using Vortex.Sdk.Shop.Logic;

namespace Vortex.Sdk.Shop.Model
{
    /// <summary>
    /// Рантайм-зеркало товара, собирается через CopyFrom из <see cref="ShopItemPreset"/> при
    /// инициализации каталога. Каталог неизменяем — GetDataForSave()=>null, всё мутабельное живёт
    /// в журнале.
    ///
    /// Свойства с protected-сеттером маппятся CopyFrom из одноимённых публичных свойств пресета;
    /// логики приходят собственными клонами через DeepCopy (основание требования stateless).
    /// </summary>
    public class ShopItemRecord : Record
    {
        /// <summary>Логика оплаты. null у пустого товара / товара-компенсации без оплаты.</summary>
        public PaymentLogic PaymentLogic { get; protected set; }

        /// <summary>Логика выдачи. null у пустого товара.</summary>
        public BuyCaseLogic BuyCaseLogic { get; protected set; }

        /// <summary>
        /// Скрыть в витрине. Статический флаг дизайнера: товары-компенсации и «удалённые» товары
        /// не показываются в обычной витрине. Движок только переносит флаг, решение о показе — на UI.
        /// </summary>
        public bool HiddenInShowcase { get; protected set; }

        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
            //Каталог неизменяем — восстанавливать нечего
        }
    }
}
