#if USING_VORTEX_SHOP
using UnityEngine;

namespace Vortex.Sdk.ShopSystem.UIBridge
{
    /// <summary>
    /// Мост кнопок UI подтверждения к <see cref="UIConfirmation"/>: висит на интерфейсе подтверждения,
    /// его публичные методы вешаются на кнопки «Подтвердить» / «Отмена» и передают вердикт в статику логики.
    /// В одной сборке с <see cref="UIConfirmation"/>, поэтому зовёт её internal-метод напрямую.
    /// </summary>
    public class UIConfirmationHandler : MonoBehaviour
    {
        /// <summary>Кнопка подтверждения: продолжить покупку.</summary>
        public void Approve() => UIConfirmation.Resolve(true);

        /// <summary>Кнопка отмены: покупка не начинается (NotStarted).</summary>
        public void Decline() => UIConfirmation.Resolve(false);
    }
}
#endif
