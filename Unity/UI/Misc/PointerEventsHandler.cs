using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Раздельное чтение UGUI-событий указателя в UnityEvent'ы: нажатие, отпускание, клик, вход, выход.
    /// Нажатие / отпускание / клик. Нужен raycast-target <c>Graphic</c> на
    /// объекте или его детях.
    ///
    /// Можно ставить на объект с <see cref="AdvancedButton"/>: UGUI отдаёт событие всем обработчикам первого
    /// объекта по иерархии — кнопка ведёт свой визуал и клик, хэндлер отдаёт раздельные нажатие и отпускание
    /// </summary>
    public class PointerEventsHandler : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("Нажатие над объектом.")]
        private UnityEvent onPointerDown;

        [SerializeField, Tooltip("Отпускание после нажатия на объекте — где бы ни был указатель.")]
        private UnityEvent onPointerUp;

        [SerializeField, Tooltip("Клик: нажатие и отпускание над объектом, без drag.")]
        private UnityEvent onPointerClick;

        [SerializeField, Tooltip("Указатель вошёл на объект.")]
        private UnityEvent onPointerEnter;

        [SerializeField, Tooltip("Указатель ушёл с объекта.")]
        private UnityEvent onPointerExit;

        public void OnPointerDown(PointerEventData eventData) => onPointerDown?.Invoke();

        public void OnPointerUp(PointerEventData eventData) => onPointerUp?.Invoke();

        public void OnPointerClick(PointerEventData eventData) => onPointerClick?.Invoke();

        public void OnPointerEnter(PointerEventData eventData) => onPointerEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => onPointerExit?.Invoke();
    }
}