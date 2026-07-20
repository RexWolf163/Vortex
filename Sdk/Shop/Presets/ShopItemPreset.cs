using UnityEngine;
using Vortex.Sdk.Shop.Logic;
using Vortex.Sdk.Shop.Model;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.Shop.Presets
{
    /// <summary>
    /// Редакторский ассет товара — единственное место его настройки. Тип записи наследуется от
    /// RecordPreset (по умолчанию Singleton): товар — единственный образец концепции, не размножается.
    ///
    /// Публичные свойства (PaymentLogic / BuyCaseLogic / HiddenInShowcase) читаются CopyFrom и
    /// маппятся в одноимённые свойства <see cref="ShopItemRecord"/>. Коэффициентов цены/выдачи здесь
    /// нет — они параметры конкретных логик.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex/Shop/Shop Item", fileName = "ShopItem")]
    public class ShopItemPreset : RecordPreset<ShopItemRecord>
    {
        [Tooltip("Логика оплаты. Задаёт, что и сколько списывать. null у товара-компенсации без оплаты.")]
        [SerializeReference]
        private PaymentLogic paymentLogic;

        [Tooltip("Логика выдачи. Задаёт, что и сколько выдавать. null у пустого товара.")]
        [SerializeReference]
        private BuyCaseLogic buyCaseLogic;

        [Tooltip("Скрыть в обычной витрине. Товары-компенсации и «удалённые» товары не показываются.")]
        [SerializeField]
        private bool hiddenInShowcase;

        public PaymentLogic PaymentLogic => paymentLogic;
        public BuyCaseLogic BuyCaseLogic => buyCaseLogic;
        public bool HiddenInShowcase => hiddenInShowcase;
    }
}
