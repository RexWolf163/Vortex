using UnityEngine;
using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Драйвер направленного ввода (источник <see cref="PointerSourceKind.Direct"/>): интегрирует вектор
    /// движения (стик/клавиши) в позицию курсора (скорость×dt, кламп к экрану) и репортит. Работает на
    /// <c>unscaledDeltaTime</c> — действует и на паузе (меню).
    /// </summary>
    public class DirectPointerDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Вектор движения — Value/Vector2 (например, <Gamepad>/leftStick).")]
        private InputActionProperty moveAction;

        [SerializeField, Tooltip("Скорость курсора, пикселей/сек.")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        private void OnEnable() => moveAction.action?.Enable();
        private void OnDisable() => moveAction.action?.Disable();

        private void Update()
        {
            var data = VirtualCursorBus.Data;
            var action = moveAction.action;
            if (data == null || action == null)
                return;

            var move = action.ReadValue<Vector2>();
            if (move.sqrMagnitude < deadzone * deadzone)
                return; // в покое — не трогаем (другой источник рулит)

            var pos = data.ScreenPosition.Value + move * (speed * Time.unscaledDeltaTime);
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
            pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
            VirtualCursorController.ReportPointer(pos, PointerSourceKind.Direct);
        }
    }
}
