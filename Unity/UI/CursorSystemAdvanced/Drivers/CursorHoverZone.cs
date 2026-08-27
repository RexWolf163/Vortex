using UnityEngine;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// UGUI-зона hover: по входу/выходу указателя ставит/снимает hover-ключ скина (аналог MouseHoverListener).
    /// Работает, т.к. UGUI-указатель ведётся виртуальным курсором (см. <see cref="UiPointerFeeder"/>).
    /// Защита от гонки вложенных зон: снимаем ключ, только если он всё ещё наш.
    /// </summary>
    public class CursorHoverZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("Ключ hover-скина (CursorSkin.Name). Пусто — зона ничего не меняет.")]
        private string hoverKey;

        public void OnPointerEnter(PointerEventData eventData) => VirtualCursorController.SetHover(hoverKey);

        public void OnPointerExit(PointerEventData eventData)
        {
            var data = VirtualCursorBus.Data;
            if (data != null && data.HoverKey.Value == hoverKey)
                VirtualCursorController.SetHover(string.Empty);
        }
    }
}
