using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Vortex.Unity.UI.VirtualCursorSystem.UiModule
{
    /// <summary>
    /// Источник ввода UGUI-модуля (<see cref="VirtualCursorInputModule"/>) поверх модели виртуального курсора.
    /// Подменяет <see cref="BaseInput"/> модуля: «мышь» для UGUI — это <c>VirtualCursorBus.Data</c>, а не устройство.
    /// <list type="bullet">
    /// <item>Позиция — <c>ScreenPosition</c>.</item>
    /// <item>Кнопки 0/1/2 — <see cref="PointerAction.BaseClick"/>/<see cref="PointerAction.ContextClick"/>/<see cref="PointerAction.AltClick"/>
    /// маски <c>Actions</c>. Фронты нажатия и отпускания фиксируются по событиям маски и держатся до конца кадра
    /// модуля (<see cref="EndFrame"/>) — нажатие и отпускание в одном кадре не теряются.</item>
    /// <item>Скролл — сумма импульсов <c>ScrollDelta</c> за кадр.</item>
    /// <item>Касания — напрямую из Input System (EnhancedTouch), только при <see cref="NativeTouch"/>; иначе касаний нет.</item>
    /// <item>Оси и кнопки навигации Selectable — всегда 0/false: навигация — фокус-подсистема пакета.</item>
    /// </list>
    /// IME (<c>compositionString</c> и т.п.) не переопределяется — работает как у стандартного <see cref="BaseInput"/>.
    /// Старый <c>UnityEngine.Input</c> не вызывается — совместимо с режимом «только Input System».
    /// </summary>
    public class VirtualCursorBaseInput : BaseInput
    {
        /// <summary>Действия маски, отдаваемые UGUI как кнопки мыши 0 (левая), 1 (правая), 2 (средняя).</summary>
        private static readonly PointerAction[] Buttons =
            { PointerAction.BaseClick, PointerAction.ContextClick, PointerAction.AltClick };

        /// <summary>Нажатие кнопки с прошлого кадра модуля.</summary>
        private readonly bool[] _down = new bool[3];

        /// <summary>Отпускание кнопки с прошлого кадра модуля.</summary>
        private readonly bool[] _up = new bool[3];

        /// <summary>Маска на момент прошлого события — для выделения фронтов.</summary>
        private PointerActionMask _prevMask;

        /// <summary>Сумма импульсов скролла с прошлого кадра модуля.</summary>
        private Vector2 _scroll;

        /// <summary>
        /// Касания идут в UGUI напрямую (мультитач). Выставляется модулем; EnhancedTouch включает модуль.
        /// </summary>
        internal bool NativeTouch { get; set; }

        /// <summary>Подписка на маску действий и скролл модели; фронты считаются от текущей маски.</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            var data = VirtualCursorBus.Data;
            _prevMask = data.Actions.Value;
            data.Actions.OnUpdate += OnActions;
            data.ScrollDelta.OnUpdate += OnScroll;
        }

        /// <summary>Отписка и сброс накопленного кадра.</summary>
        protected override void OnDisable()
        {
            var data = VirtualCursorBus.Data;
            data.Actions.OnUpdate -= OnActions;
            data.ScrollDelta.OnUpdate -= OnScroll;
            EndFrame();
            base.OnDisable();
        }

        /// <summary>Смена маски — зафиксировать фронты нажатия и отпускания каждой кнопки.</summary>
        private void OnActions(PointerActionMask mask)
        {
            for (var i = 0; i < Buttons.Length; i++)
            {
                var was = _prevMask.IsActive(Buttons[i]);
                var now = mask.IsActive(Buttons[i]);
                if (!was && now) _down[i] = true;
                if (was && !now) _up[i] = true;
            }

            _prevMask = mask;
        }

        /// <summary>Импульс скролла — добавить к сумме кадра.</summary>
        private void OnScroll(Vector2 delta) => _scroll += delta;

        /// <summary>Сбросить фронты и скролл кадра. Зовётся модулем после обработки кадра.</summary>
        internal void EndFrame()
        {
            for (var i = 0; i < Buttons.Length; i++)
                _down[i] = _up[i] = false;
            _scroll = Vector2.zero;
        }

        /// <summary>Виртуальный курсор есть всегда.</summary>
        public override bool mousePresent => true;

        /// <summary>Позиция виртуального курсора.</summary>
        public override Vector2 mousePosition => VirtualCursorBus.Data.ScreenPosition.Value;

        /// <summary>Сумма импульсов скролла за кадр.</summary>
        public override Vector2 mouseScrollDelta => _scroll;

        /// <summary>Импульсы уже в единицах UGUI — масштаб 1.</summary>
        public override float mouseScrollDeltaPerTick => 1f;

        /// <summary>Кнопка зажата в текущей маске.</summary>
        public override bool GetMouseButton(int button) =>
            IsButton(button) && VirtualCursorBus.Data.Actions.Value.IsActive(Buttons[button]);

        /// <summary>Кнопка нажата с прошлого кадра модуля.</summary>
        public override bool GetMouseButtonDown(int button) => IsButton(button) && _down[button];

        /// <summary>Кнопка отпущена с прошлого кадра модуля.</summary>
        public override bool GetMouseButtonUp(int button) => IsButton(button) && _up[button];

        /// <summary>Касания поддержаны, если включены и есть тач-экран.</summary>
        public override bool touchSupported => NativeTouch && UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled;

        /// <summary>Активные касания кадра (включая закончившиеся в нём); без нативных касаний — 0.</summary>
        public override int touchCount => touchSupported ? ETouch.activeTouches.Count : 0;

        /// <summary>Касание Input System в формате <see cref="Touch"/> для UGUI; палец — <c>touchId</c>.</summary>
        public override Touch GetTouch(int index)
        {
            var t = ETouch.activeTouches[index];
            return new Touch
            {
                fingerId = t.touchId,
                position = t.screenPosition,
                rawPosition = t.screenPosition,
                deltaPosition = t.delta,
                tapCount = t.tapCount,
                phase = ToPhase(t.phase),
                pressure = t.pressure,
                maximumPossiblePressure = 1f,
                radius = Mathf.Max(t.radius.x, t.radius.y),
                type = TouchType.Direct,
            };
        }

        /// <summary>Оси навигации Selectable не используются.</summary>
        public override float GetAxisRaw(string axisName) => 0f;

        /// <summary>Кнопки навигации Selectable не используются.</summary>
        public override bool GetButtonDown(string buttonName) => false;

        /// <summary>Индекс кнопки мыши, которую отдаёт этот источник (0..2).</summary>
        private static bool IsButton(int button) => button >= 0 && button < Buttons.Length;

        /// <summary>Фаза касания Input System → фаза UGUI.</summary>
        private static TouchPhase ToPhase(UnityEngine.InputSystem.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: return TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved: return TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Ended: return TouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Canceled: return TouchPhase.Canceled;
                default: return TouchPhase.Stationary;
            }
        }
    }
}
