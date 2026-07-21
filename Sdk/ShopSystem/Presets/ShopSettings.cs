#if USING_VORTEX_SHOP
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ShopSystem.Model;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Sdk.ShopSystem.Presets
{
    /// <summary>
    /// Настройки движка магазина.
    /// Единственный параметр — политика поведения после оплаты, глобальная на игру.
    /// Снапшот политики в журнал не делается: открытая покупка доигрывает по текущим настройкам.
    /// </summary>
    [Serializable]
    public class ShopSettings : ScriptableObject, ICoreAsset
    {
        [InfoBox("Поведение после подтверждённой оплаты:\nRollback — автовыдача, откат при сбое;\n" +
                 "Pending — автовыдача, удержание для повтора;\nReady — ждать подтверждения игроком.")]
        [SerializeField]
        private AfterpayMode afterpayMode = AfterpayMode.Rollback;

        public AfterpayMode AfterpayMode => afterpayMode;
    }
}
#endif