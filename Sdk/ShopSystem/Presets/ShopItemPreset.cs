#if USING_VORTEX_SHOP
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.ShopSystem.Model;
using Vortex.Sdk.ShopSystem.Model.Logics;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.ShopSystem.Presets
{
    /// <summary>
    /// Пресет товара магазина — authoring-поверхность для <see cref="ShopItemModel"/>. Логики оплаты
    /// и выдачи задаются полиморфно через [SerializeReference]; имена свойств совпадают со свойствами
    /// записи, поэтому их переносит CopyFrom при построении. Тип записи закреплён как Singleton —
    /// каталог неизменяем.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItemModel", menuName = "Database/ShopItem")]
    public class ShopItemPreset : RecordPreset<ShopItemModel>
    {
        [SerializeReference, HideReferenceObjectPicker]
        private PaymentLogic paymentLogic;

        public PaymentLogic PaymentLogic => paymentLogic;

        [SerializeReference, HideReferenceObjectPicker]
        private DeliveryLogic deliveryLogic;

        public DeliveryLogic DeliveryLogic => deliveryLogic;

        [SerializeField] private bool hiddenInShowcase;

        public bool HiddenInShowcase => hiddenInShowcase;

#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.Singleton;
#endif
    }
}
#endif