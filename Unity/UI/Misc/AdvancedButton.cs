using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Класс "доработанной" кнопки.
    /// Транслирует события Нажал, Отпустил, Вошел в границы и вышел за границы
    /// </summary>
    public class AdvancedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        /*
        /// <summary>
        /// Время для фиксации клика в миллисекундах
        /// </summary>
        private const float TimeForClickMs = 100f;
        */

        /// <summary>
        /// Максимальное смещение для фиксации клика в пикселях
        /// </summary>
        private const float ShiftForClickLess = 20f;

        /// <summary>
        /// Способ фиксации кликов
        /// </summary>
        private enum ClickRegType
        {
            OnTap,
            OnUpInBorders,
            OnUpAnywhere,
            OnClick, //Нажата и отпущена быстрее чем через 0,1s без смещения больше чем на 10 пикселей
        }

        private enum ButtonVisualState
        {
            Free,
            Hover,
            Pressed
        }

        /// <summary>
        /// Клик зарегистрирован согласно выбранной схеме
        /// </summary>
        public event Action OnClick;

        /// <summary>
        /// Нажатие на кнопку
        /// </summary>
        public event Action OnPressed;

        /// <summary>
        /// Кнопка отпущена
        /// </summary>
        public event Action OnReleased;

        /// <summary>
        /// Курсор над кнопкой
        /// </summary>
        public event Action OnHover;

        /// <summary>
        /// курсор покинул кнопку
        /// </summary>
        public event Action OnExit;

        [SerializeField] private UnityEvent[] onClick;
        [SerializeField] private UnityEvent[] onHover;
        [SerializeField] private UnityEvent[] onExit;

        [SerializeField] private ClickRegType clickRegType = ClickRegType.OnClick;

        [SerializeField, StateSwitcher(typeof(ButtonVisualState))]
        private UIStateSwitcher uiStateSwitcher;

        private bool _pressed = false;

        private bool _inBorders = false;

        //private DateTime _clickTime;

        private Vector2 _clickCoords;

        private void OnDisable()
        {
            _inBorders = false;
            Set(ButtonVisualState.Free);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _inBorders = true;
            foreach (var act in onHover)
                act?.Invoke();

            Set(ButtonVisualState.Hover);

            OnHover?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _inBorders = false;
            foreach (var act in onExit)
                act?.Invoke();
            if (!_pressed)
                Set(ButtonVisualState.Free);

            OnExit?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (clickRegType == ClickRegType.OnTap)
                Click();

            Set(ButtonVisualState.Pressed);

            //_clickTime = DateTime.Now;
            _clickCoords = eventData?.position ?? Vector2.zero;

            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (clickRegType == ClickRegType.OnUpAnywhere || _inBorders && clickRegType == ClickRegType.OnUpInBorders)
                Click();

            if (clickRegType == ClickRegType.OnClick
                //&& (DateTime.Now - _clickTime).TotalMilliseconds < TimeForClickMs
                && (_clickCoords - eventData.position).magnitude < ShiftForClickLess)
                Click();

            Set(_inBorders ? ButtonVisualState.Hover : ButtonVisualState.Free);

            OnReleased?.Invoke();
        }

        private void Click()
        {
            foreach (var act in onClick)
                act?.Invoke();

            OnClick?.Invoke();
        }

        private void Set(ButtonVisualState state) => uiStateSwitcher?.Set(state);

        /// <summary>
        /// Внешнее управление нажатием
        /// </summary>
        public void Press() => OnPointerDown(null);

        /// <summary>
        /// Внешнее управление нажатием
        /// </summary>
        public void Release() => OnPointerUp(null);

        public void AddOnClick(UnityAction currentAction) => OnClick += currentAction.Invoke;

        public void RemoveOnClick(UnityAction currentAction) => OnClick -= currentAction.Invoke;
    }
}