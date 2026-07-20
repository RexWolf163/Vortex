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
        [Tooltip("Поведение после подтверждённой оплаты. Rollback — откат при сбое выдачи; " +
                 "Pending — удержание для повтора; Ready — ждать подтверждения игроком.")]
        [SerializeField]
        private FallbackMode fallbackMode = FallbackMode.Rollback;

        public FallbackMode FallbackMode => fallbackMode;
    }
}
