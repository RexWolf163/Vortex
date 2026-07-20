using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Presets
{
    /// <summary>
    /// Настройки движка магазина. Частные данные системы — живёт отдельным ассетом, вне Database.
    /// Единственный параметр — политика поведения после оплаты, глобальная на игру.
    /// Снапшот политики в журнал не делается: открытая покупка доигрывает по текущим настройкам.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex/Shop/Shop Settings", fileName = "ShopSettings")]
    public class ShopSettings : ScriptableObject
    {
        [InfoBox("Поведение после подтверждённой оплаты:\nRollback — автовыдача, откат при сбое;\n" +
                 "Pending — автовыдача, удержание для повтора;\nReady — ждать подтверждения игроком.")]
        [SerializeField]
        private AfterpayMode afterpayMode = AfterpayMode.Rollback;

        public AfterpayMode AfterpayMode => afterpayMode;
    }
}