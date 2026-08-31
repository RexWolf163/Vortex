using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.InputBusSystem;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Драйвер курсора от привязанных экшенов, кооперативный с мышью. Двигает НАПРЯМУЮ реальную системную мышь
    /// (<see cref="Mouse.current"/>) по вектору из <see cref="moveActionId"/> и жмёт <see cref="leftButtonActionId"/> /
    /// <see cref="rightButtonActionId"/> как LMB/RMB. Одно устройство — нет рассинхрона: и видимый курсор
    /// (<see cref="CursorController"/> рисует по позиции ОС-курсора), и UI-модуль, и клики читают ту же мышь.
    ///
    /// Ввод гонится СОБЫТИЕМ (<c>QueueStateEvent(MouseState)</c>) — позиция + кнопки одним состоянием. Через
    /// событийный пайплайн клики корректно дают <c>WasPressedThisFrame</c>, и родной UGUI-клик срабатывает
    /// (в отличие от <c>InputState.Change</c> из Update, который значение пишет, но фронт «нажато в этом кадре»
    /// не выставляет — из-за этого геймпад-кнопка как ЛКМ не кликала). Событие шлётся только когда геймпад
    /// активен (двигает/жмёт); в простое мышь работает нативно.
    ///
    /// Привязки — строковые id экшенов из карты (дропдаун), резолвятся через <see cref="InputController"/> и
    /// читаются polling'ом (ReadValue/IsPressed). Требует наличие мыши как устройства (десктоп).
    /// </summary>
    public class GamepadCursorDriver : MonoBehaviour
    {
        [Header("Привязки ввода (id «Карта/Экшен»)")]
        [SerializeField, ValueSelector("GetInputActions"), Tooltip("Движение курсора — Value/Vector2 (например, левый стик геймпада).")]
        private string moveActionId;

        [SerializeField, ValueSelector("GetInputActions"), Tooltip("Левый клик — Button (например, кнопка South).")]
        private string leftButtonActionId;

        [SerializeField, ValueSelector("GetInputActions"), Tooltip("Правый клик — Button (например, кнопка East).")]
        private string rightButtonActionId;

        [SerializeField, ValueSelector("GetInputActions"), Tooltip("Скролл — Value/Vector2 (например, правый стик / дпад).")]
        private string scrollActionId;

        [Header("Движение")]
        [SerializeField, Tooltip("Скорость курсора, пикселей/сек.")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        [SerializeField, Min(0),
         Tooltip("Грейс на отпускание кнопки (кадров): кратковременные провалы ввода (аналоговый триггер как Button у порога) не срывают удержание/клик.")]
        private int releaseGraceFrames = 3;

        [SerializeField, Min(0f), Tooltip("Скорость скролла — масштаб вектора в кадровую дельту.")]
        private float scrollSpeed = 20f;

        private InputAction _moveAction;
        private InputAction _leftAction;
        private InputAction _rightAction;
        private InputAction _scrollAction;

        private bool _leftHeld;
        private bool _rightHeld;
        private int _leftRelease;
        private int _rightRelease;
        private int _injected; // биты кнопок, которые МЫ инжектнули в прошлом кадре (для OR-мержа без залипания)

        private void OnEnable()
        {
            _moveAction = InputController.GetAction(moveActionId);
            _leftAction = InputController.GetAction(leftButtonActionId);
            _rightAction = InputController.GetAction(rightButtonActionId);
            _scrollAction = InputController.GetAction(scrollActionId);

            _moveAction?.Enable();
            _leftAction?.Enable();
            _rightAction?.Enable();
            _scrollAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _leftAction?.Disable();
            _rightAction?.Disable();
            _scrollAction?.Disable();
            _leftHeld = false;
            _rightHeld = false;
            _leftRelease = 0;
            _rightRelease = 0;

            // Снять зависшую инъекцию геймпад-кнопок, сохранив физические.
            var mouse = Mouse.current;
            if (_injected != 0 && mouse != null)
            {
                var physical = ReadButtons(mouse) & ~_injected;
                InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = mouse.position.ReadValue(), buttons = (ushort)physical });
            }

            _injected = 0;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            // --- Движение стиком: только ПОЗИЦИЯ, кнопки не трогаем (физические кнопки/клики мышью текут сами). ---
            var move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (move.sqrMagnitude >= deadzone * deadzone)
            {
                var pos = mouse.position.ReadValue() + move * (speed * Time.unscaledDeltaTime);
                pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
                pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
                InputState.Change(mouse.position, pos); // значение для UI/CursorSystem — сразу, без трогания кнопок
                mouse.WarpCursorPosition(pos);          // видимый ОС-курсор
            }

            // --- Геймпад-кнопки с грейсом на отпускание (гасит «срыв» на дрожи триггера у порога). ---
            var gpLeft = Hold(IsPressed(_leftAction), ref _leftHeld, ref _leftRelease);
            var gpRight = Hold(IsPressed(_rightAction), ref _rightHeld, ref _rightRelease);
            var gp = (gpLeft ? LeftBit : 0) | (gpRight ? RightBit : 0);

            // --- Скролл: вне мёртвой зоны — масштабируем в кадровую дельту. ---
            var rawScroll = _scrollAction != null ? _scrollAction.ReadValue<Vector2>() : Vector2.zero;
            var scroll = rawScroll.sqrMagnitude >= deadzone * deadzone
                ? rawScroll * (scrollSpeed * Time.unscaledDeltaTime)
                : Vector2.zero;

            // Геймпад-кнопки не участвуют, наша инъекция снята и скролла нет → buttons НЕ трогаем: физические
            // кнопки (и клики мышью при движении геймпадом) работают нативно.
            if (gp == 0 && _injected == 0 && scroll == Vector2.zero)
                return;

            // OR-гейт: физические кнопки = состояние девайса МИНУС наша прошлая инъекция; мержим с геймпадом.
            // Так «мышь-кнопки + геймпад-курсор» и наоборот работают вместе, а на отпускании геймпад-кнопки
            // не залипают (вычитание _injected снимает нашу же инъекцию, не трогая физические). + скролл.
            var physical = ReadButtons(mouse) & ~_injected;
            var merged = physical | gp;

            InputSystem.QueueStateEvent(mouse,
                new MouseState { position = mouse.position.ReadValue(), buttons = (ushort)merged, scroll = scroll });
            _injected = gp;
        }

        private static bool IsPressed(InputAction action) => action != null && action.IsPressed();

        // Нажатие — сразу; отпускание — только после releaseGraceFrames подряд «не нажато».
        private bool Hold(bool pressed, ref bool held, ref int releaseStreak)
        {
            if (pressed)
            {
                held = true;
                releaseStreak = 0;
            }
            else if (held && ++releaseStreak > releaseGraceFrames)
            {
                held = false;
                releaseStreak = 0;
            }

            return held;
        }

        private const int LeftBit = 1 << (int)MouseButton.Left;
        private const int RightBit = 1 << (int)MouseButton.Right;
        private const int MiddleBit = 1 << (int)MouseButton.Middle;
        private const int BackBit = 1 << (int)MouseButton.Back;
        private const int ForwardBit = 1 << (int)MouseButton.Forward;

        // Битовая маска физически нажатых кнопок мыши (все 5) — чтобы OR-мерж сохранял любой физический ввод.
        private static int ReadButtons(Mouse m)
        {
            var b = 0;
            if (m.leftButton.isPressed) b |= LeftBit;
            if (m.rightButton.isPressed) b |= RightBit;
            if (m.middleButton.isPressed) b |= MiddleBit;
            if (m.backButton.isPressed) b |= BackBit;
            if (m.forwardButton.isPressed) b |= ForwardBit;
            return b;
        }

#if UNITY_EDITOR
        private string[] GetInputActions() => InputController.GetActions();
#endif
    }
}
