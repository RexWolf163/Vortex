using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Драйвер курсора от привязанных экшенов, кооперативный с мышью. Двигает НАПРЯМУЮ реальную системную мышь
    /// (<see cref="Mouse.current"/>) по вектору из <see cref="moveAction"/> и жмёт <see cref="leftButtonAction"/> /
    /// <see cref="rightButtonAction"/> как LMB/RMB. Одно устройство — нет рассинхрона: и видимый курсор
    /// (<see cref="CursorController"/> рисует по позиции ОС-курсора), и UI-модуль, и клики читают ту же мышь.
    ///
    /// Ввод гонится СОБЫТИЕМ (<c>QueueStateEvent(MouseState)</c>) — позиция + кнопки одним состоянием. Через
    /// событийный пайплайн клики корректно дают <c>WasPressedThisFrame</c>, и родной UGUI-клик срабатывает
    /// (в отличие от <c>InputState.Change</c> из Update, который значение пишет, но фронт «нажато в этом кадре»
    /// не выставляет — из-за этого геймпад-кнопка как ЛКМ не кликала). Событие шлётся только когда геймпад
    /// активен (двигает/жмёт); в простое мышь работает нативно.
    ///
    /// Привязки — <see cref="InputActionProperty"/> (инлайн-экшен или ссылка), выбираются в инспекторе.
    /// Требует наличие мыши как устройства (десктоп).
    /// </summary>
    public class GamepadCursorDriver : MonoBehaviour
    {
        [Header("Привязки ввода")]
        [SerializeField, Tooltip("Движение курсора — Value/Vector2 (например, левый стик геймпада).")]
        private InputActionProperty moveAction;

        [SerializeField, Tooltip("Левый клик — Button (например, кнопка South).")]
        private InputActionProperty leftButtonAction;

        [SerializeField, Tooltip("Правый клик — Button (например, кнопка East).")]
        private InputActionProperty rightButtonAction;

        [Header("Движение")]
        [SerializeField, Tooltip("Скорость курсора, пикселей/сек.")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        private bool _leftHeld;
        private bool _rightHeld;

        private void OnEnable()
        {
            moveAction.action?.Enable();
            leftButtonAction.action?.Enable();
            rightButtonAction.action?.Enable();
        }

        private void OnDisable()
        {
            moveAction.action?.Disable();
            leftButtonAction.action?.Disable();
            rightButtonAction.action?.Disable();
            _leftHeld = false;
            _rightHeld = false;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var move = moveAction.action != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            var moving = move.sqrMagnitude >= deadzone * deadzone;

            var left = IsPressed(leftButtonAction);
            var right = IsPressed(rightButtonAction);

            // Геймпад не активен (не двигает, не жмёт, нет незакрытого удержания) — мышь работает нативно.
            if (!moving && !left && !right && !_leftHeld && !_rightHeld)
                return;

            var pos = mouse.position.ReadValue();
            if (moving)
            {
                pos += move * (speed * Time.unscaledDeltaTime);
                pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
                pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
                mouse.WarpCursorPosition(pos); // видимый ОС-курсор
            }

            // Полное состояние мыши событием: позиция + кнопки. Событийный пайплайн даёт корректные фронты →
            // UGUI-клик и Action-спрайт CursorSystem срабатывают. Release-фронт тоже уйдёт: пока _xHeld висит,
            // геймпад считается активным и в кадре отпускания шлётся состояние с уже снятой кнопкой.
            var state = new MouseState { position = pos };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);

            _leftHeld = left;
            _rightHeld = right;
        }

        private static bool IsPressed(InputActionProperty prop) => prop.action != null && prop.action.IsPressed();
    }
}
