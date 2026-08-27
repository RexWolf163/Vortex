using UnityEngine;
using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Драйвер мыши (источник <see cref="PointerSourceKind.Analog"/>): событийно читает позицию и репортит её.
    /// Порог активности живёт здесь (специфика мыши) и гейтит ТОЛЬКО перехват: пока мышь не активный источник,
    /// забирает управление лишь при заметном сдвиге (джиттер не крадёт курсор у Direct); активная — трекает
    /// каждое движение без порога. Игнорирует события своего же <see cref="VirtualUiPointer"/> (feedback-петля).
    /// </summary>
    public class MousePointerDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Позиция указателя — Value/Vector2 (например, <Mouse>/position).")]
        private InputActionProperty positionAction;

        [SerializeField, Min(0.1f), Tooltip("Порог перехвата (квадрат сдвига в пикселях).")]
        private float activationThresholdSqr = 4f;

        private Vector2 _lastPoint;

        private void OnEnable()
        {
            var a = positionAction.action;
            if (a == null) return;
            a.Enable();
            a.performed += OnPosition;
        }

        private void OnDisable()
        {
            var a = positionAction.action;
            if (a == null) return;
            a.performed -= OnPosition;
            a.Disable();
        }

        private void OnPosition(InputAction.CallbackContext ctx)
        {
            if (ctx.control?.device is VirtualUiPointer)
                return; // свой виртуальный указатель — не реагируем

            var data = VirtualCursorBus.Data;
            if (data == null) return;

            var pos = ctx.ReadValue<Vector2>();

            if (data.ActiveSource.Value == PointerSourceKind.Analog)
            {
                _lastPoint = pos;
                VirtualCursorController.ReportPointer(pos, PointerSourceKind.Analog);
                return;
            }

            if ((pos - _lastPoint).sqrMagnitude < activationThresholdSqr)
                return;

            _lastPoint = pos;
            VirtualCursorController.ReportPointer(pos, PointerSourceKind.Analog);
        }
    }
}
