#if USING_VORTEX_CURSOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер касания (источник <see cref="PointerSourceKind.Point"/>). Прямой контакт — курсор для него
    /// не нужен, поэтому <see cref="HidesCursor"/> = true (контроллер по last-source-wins скроет визуал).
    /// </summary>
    [Serializable]
    public class TouchInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Позиция касания — Value/Vector2 (например, Touchscreen/primaryTouch/position).")]
        private string pointActionId;

        private InputAction _action;

        public override bool HidesCursor => true;

        public override void Connect()
        {
            _action = ResolveAction(pointActionId);
            EnableMap(pointActionId);
            SubscribeAction(pointActionId, OnPoint, null);
        }

        public override void Disconnect()
        {
            UnsubscribeAction(pointActionId);
            DisableMap(pointActionId);
            _action = null;
        }

        private void OnPoint()
        {
            if (_action == null) return;
            Report(_action.ReadValue<Vector2>(), PointerSourceKind.Point);
        }
    }
}
#endif
