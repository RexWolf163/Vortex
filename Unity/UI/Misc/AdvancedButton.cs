using System;
using System.Collections.Generic;
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
        IPointerUpHandler, IBeginDragHandler, IEndDragHandler
    {
        /// <summary>
        /// Время для фиксации клика в миллисекундах
        /// </summary>
        private const float TimeForClickMs = 200f;

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

        private DateTime _clickTime;

        private Vector2 _clickCoords;

        /// <summary>
        /// Был ли в текущем pointer-цикле инициирован drag (скролл-контейнер протащил указатель).
        /// Поднимается в <see cref="OnBeginDrag"/>, гасится в <see cref="OnEndDrag"/> и
        /// в <see cref="OnPointerUp"/>. Используется для подавления регистрации клика,
        /// когда нажатие на кнопку было частью скролла.
        /// </summary>
        private bool _dragged;

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

            // B-2: если палец/курсор всё ещё прижат, возвращаем визуал в Pressed,
            // а не сбрасываем в Hover. Сценарий: нажали → увели за пределы → вернули обратно.
            Set(_pressed ? ButtonVisualState.Pressed : ButtonVisualState.Hover);

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

            _clickTime = DateTime.Now;
            _clickCoords = eventData?.position ?? Vector2.zero;

            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;

            // Драг-фильтр: если в этом pointer-цикле был замечен drag (наш собственный
            // флаг от IBeginDragHandler) или EventSystem помечает событие как drag
            // (eventData.dragging) или указатель «перенаправлен» на чужой объект
            // (eventData.pointerDrag != нашему gameObject — типичный случай, когда
            // протаскивание подхватил родительский ScrollRect) — клик НЕ регистрируем.
            // OnTap из-под этой защиты исключён намеренно: он срабатывает в OnPointerDown,
            // до того как EventSystem успевает классифицировать движение как drag.
            var draggedThisCycle = _dragged
                                   || (eventData != null
                                       && (eventData.dragging
                                           || (eventData.pointerDrag != null
                                               && eventData.pointerDrag != gameObject)));

            if (!draggedThisCycle)
            {
                if (clickRegType == ClickRegType.OnUpAnywhere
                    || _inBorders && clickRegType == ClickRegType.OnUpInBorders)
                    Click();

                // B-1: считаем точку отпускания корректно даже при eventData == null
                // (внешний Release()). Старая формула из-за приоритета операторов давала
                // null в Vector2?-вычитании, ?? подставлял _clickCoords, и magnitude
                // оказывалась расстоянием от (0,0) до точки нажатия — клик никогда не
                // регистрировался в режиме OnClick для кнопок не у левого верхнего угла.
                var releasePos = eventData?.position ?? _clickCoords;
                if (clickRegType == ClickRegType.OnClick
                    && (DateTime.Now - _clickTime).TotalMilliseconds < TimeForClickMs
                    && (_clickCoords - releasePos).magnitude < ShiftForClickLess)
                    Click();
            }

            _dragged = false;

            Set(_inBorders ? ButtonVisualState.Hover : ButtonVisualState.Free);

            OnReleased?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData) => _dragged = true;

        public void OnEndDrag(PointerEventData eventData) => _dragged = false;

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

        private Dictionary<UnityAction, Action> _wrappedActions;

        public void AddOnClick(UnityAction currentAction)
        {
            _wrappedActions ??= new Dictionary<UnityAction, Action>();
            // B-3: защита от двойной подписки одного UnityAction. Без неё повторный
            // AddOnClick перезаписывал словарь новым wrapper'ом, а старый wrapper
            // оставался подписан на OnClick — RemoveOnClick его уже не находил.
            if (_wrappedActions.ContainsKey(currentAction)) return;
            Action wrapper = currentAction.Invoke;
            _wrappedActions[currentAction] = wrapper;
            OnClick += wrapper;
        }

        public void RemoveOnClick(UnityAction currentAction)
        {
            if (_wrappedActions == null || !_wrappedActions.Remove(currentAction, out var wrapper)) return;
            OnClick -= wrapper;
        }
    }
}