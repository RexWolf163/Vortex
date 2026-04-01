using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UIProviderSystem.Handlers
{
    /// <summary>
    /// Хэндлер перетаскивания UI.
    /// Линкуется снаружи, из управляемого элемента.
    /// Генерирует событие с вектором смещения
    /// </summary>
    public class UIDragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private enum DragState
        {
            Free,
            Dragging,
        }

        public event Action OnDown;
        public event Action OnUp;
        public event Action<Vector2> OnDrag;

        [SerializeField, StateSwitcher(typeof(DragState))]
        private UIStateSwitcher uiStateSwitcher;

        private Vector2 _oldPosition = Vector2.zero;

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            OnDrag?.Invoke(eventData.position - _oldPosition);
            _oldPosition = eventData.position;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDown?.Invoke();
            _oldPosition = eventData.position;
            if (uiStateSwitcher != null)
                uiStateSwitcher.Set(DragState.Dragging);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnUp?.Invoke();
            if (uiStateSwitcher != null)
                uiStateSwitcher.Set(DragState.Free);
        }
    }
}