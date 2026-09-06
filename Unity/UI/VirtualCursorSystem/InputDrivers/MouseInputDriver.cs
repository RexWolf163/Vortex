#if USING_VORTEX_CURSOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер мыши (источник <see cref="PointerSourceKind.Analog"/>). Событийно репортит абсолютную позицию.
    /// Курсор не скрывает.
    /// </summary>
    [Serializable]
    public class MouseInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Позиция мыши — Value/Vector2 (например, Mouse/position).")]
        private string positionActionId;

        private InputAction _action;

        public override void Connect()
        {
            _action = ResolveAction(positionActionId);
            EnableMap(positionActionId);
            SubscribeAction(positionActionId, OnMove, null);
        }

        public override void Disconnect()
        {
            UnsubscribeAction(positionActionId);
            DisableMap(positionActionId);
            _action = null;
        }

        private void OnMove()
        {
            if (_action == null) return;
            Report(_action.ReadValue<Vector2>(), PointerSourceKind.Analog);
        }
    }
}
#endif
