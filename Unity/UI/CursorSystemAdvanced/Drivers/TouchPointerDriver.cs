using UnityEngine;
using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Драйвер касания (источник <see cref="PointerSourceKind.Point"/>): абсолютная точка контакта репортится
    /// напрямую. Бинд — позиция касания (например, <c>&lt;Touchscreen&gt;/primaryTouch/position</c>).
    /// </summary>
    public class TouchPointerDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Позиция касания — Value/Vector2.")]
        private InputActionProperty pointAction;

        private void OnEnable()
        {
            var a = pointAction.action;
            if (a == null) return;
            a.Enable();
            a.performed += OnPoint;
        }

        private void OnDisable()
        {
            var a = pointAction.action;
            if (a == null) return;
            a.performed -= OnPoint;
            a.Disable();
        }

        private void OnPoint(InputAction.CallbackContext ctx)
        {
            if (VirtualCursorBus.Data == null) return;
            VirtualCursorController.ReportPointer(ctx.ReadValue<Vector2>(), PointerSourceKind.Point);
        }
    }
}
