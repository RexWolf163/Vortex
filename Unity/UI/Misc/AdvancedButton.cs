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
        IPointerUpHandler, IPointerClickHandler
    {
        /// <summary>
        /// Максимальная длительность нажатия (мс), при которой release ещё считается частью
        /// клика в режиме <see cref="ClickRegType.OnClick"/>. Дольше — считается «удержанием»,
        /// клик не регистрируется. Дистанционный фильтр (drag) выполняется самим EventSystem
        /// через <see cref="EventSystems.EventSystem.pixelDragThreshold"/> — настраивается
        /// в EventSystem-компоненте сцены.
        /// </summary>
        private const float TimeForClickMs = 200f;

        /// <summary>
        /// Способ фиксации кликов
        /// </summary>
        private enum ClickRegType
        {
            OnTap,
            OnUpInBorders,
            OnUpAnywhere,
            OnClick, // Нажата и отпущена быстрее чем за TimeForClickMs, и EventSystem не классифицировала жест как drag
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

        private void OnEnable()
        {
            _pressed = false;
            uiStateSwitcher?.ResetStates();
            Set(ButtonVisualState.Free);
        }

        private void OnDisable()
        {
            _inBorders = false;
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

            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;

            // OnUpAnywhere: контракт режима — release фиксируется где угодно, даже если
            // EventSystem классифицировала жест как drag. Если такое поведение нежелательно
            // (типичный кейс — кнопка внутри ScrollRect), используй ClickRegType.OnClick
            // или ClickRegType.OnUpInBorders: они идут через IPointerClickHandler и
            // автоматически отсеивают drag.
            if (clickRegType == ClickRegType.OnUpAnywhere)
                Click();

            // Внешний программный клик через Press()/Release(). EventSystem
            // IPointerClickHandler в этом случае не вызовет (это не пользовательский input),
            // поэтому режим OnClick/OnUpInBorders требует ручной обработки. Время и
            // дистанцию проверяем — это согласовано с правилами режима OnClick.
            if (eventData == null
                && (clickRegType == ClickRegType.OnClick || clickRegType == ClickRegType.OnUpInBorders)
                && (DateTime.Now - _clickTime).TotalMilliseconds < TimeForClickMs)
                Click();

            Set(_inBorders ? ButtonVisualState.Hover : ButtonVisualState.Free);

            OnReleased?.Invoke();
        }

        /// <summary>
        /// Канонический сигнал «это был клик, а не drag» от UGUI EventSystem.
        /// Гарантии Unity:
        ///   • OnPointerDown был на этом же объекте;
        ///   • OnPointerUp произошёл, пока указатель находился над этим объектом;
        ///   • в цикле жеста никто не получил OnBeginDrag (ни эта кнопка, ни родители),
        ///     то есть смещение не превысило <see cref="EventSystems.EventSystem.pixelDragThreshold"/>;
        ///   • eventData.eligibleForClick === true.
        /// Если кнопка лежит в <c>ScrollRect</c>, при скролле родитель получит drag, и
        /// этот метод не будет вызван вовсе — клик корректно подавится без дополнительных
        /// флагов.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickRegType == ClickRegType.OnUpInBorders)
            {
                Click();
                return;
            }

            if (clickRegType == ClickRegType.OnClick
                && (DateTime.Now - _clickTime).TotalMilliseconds < TimeForClickMs)
                Click();
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
        public void Release()
        {
            if (!_pressed)
                return;
            OnPointerUp(null);
        }

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