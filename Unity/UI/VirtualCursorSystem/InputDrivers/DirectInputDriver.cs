#if USING_VORTEX_CURSOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер направленного ввода (источник <see cref="PointerSourceKind.Direct"/>): интегрирует вектор
    /// движения (стик/клавиши) в позицию курсора в <see cref="Tick"/>. Работает на unscaledDeltaTime —
    /// действует и на паузе (меню). Курсор не скрывает.
    /// </summary>
    [Serializable]
    public class DirectInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Вектор движения — Value/Vector2 (например, Gamepad/leftStick).")]
        private string moveActionId;

        [SerializeField, Tooltip("Скорость курсора, пикселей/сек.")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        private InputAction _action;

        public override bool NeedsTick => true;

        public override void Connect()
        {
            _action = ResolveAction(moveActionId);
            EnableMap(moveActionId);
        }

        public override void Disconnect()
        {
            DisableMap(moveActionId);
            _action = null;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (_action == null || VirtualCursorBus.Data == null) return;

            var move = _action.ReadValue<Vector2>();
            if (move.sqrMagnitude < deadzone * deadzone)
                return; // в покое — не трогаем (рулит другой источник)

            var pos = VirtualCursorBus.Data.ScreenPosition.Value + move * (speed * unscaledDeltaTime);
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
            pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
            Report(pos, PointerSourceKind.Direct);
        }
    }
}
#endif
