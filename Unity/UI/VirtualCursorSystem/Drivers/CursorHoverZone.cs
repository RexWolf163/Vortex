using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// UGUI-зона hover: по входу/выходу указателя ставит/снимает hover-ключ скина (аналог MouseHoverListener).
    /// Работает, т.к. UGUI-указатель ведётся виртуальным курсором — события
    /// <c>IPointerEnterHandler</c>/<c>IPointerExitHandler</c> шлёт
    /// <see cref="VirtualPointerDispatcher"/> напрямую через <c>ExecuteEvents</c>.
    /// Защита от гонки вложенных зон: снимаем ключ, только если он всё ещё наш.
    /// </summary>
    public class CursorHoverZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("Ключ hover-скина (CursorSkin.Name). Пусто — зона ничего не меняет.")]
        private string hoverKey;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(hoverKey)) return; // пусто = зона ничего не меняет (контракт Tooltip)
            VirtualCursorController.SetHover(hoverKey);
        }

        public void OnPointerExit(PointerEventData eventData) => ClearIfMine();

        // Снять свой ключ при выключении/уничтожении зоны под курсором: UGUI не шлёт Exit на
        // деактивируемый объект, из-за чего ключ hover залипал бы, пока курсор не войдёт в другую зону.
        private void OnDisable() => ClearIfMine();

        // Снимаем ключ, только если он всё ещё наш (защита от гонки вложенных зон).
        private void ClearIfMine()
        {
            if (string.IsNullOrEmpty(hoverKey)) return;
            var data = VirtualCursorBus.Data;
            if (data != null && data.HoverKey.Value == hoverKey)
                VirtualCursorController.SetHover(string.Empty);
        }
    }
}
