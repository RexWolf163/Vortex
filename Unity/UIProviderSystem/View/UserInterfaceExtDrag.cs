using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.UIProviderSystem.Handlers;

namespace Vortex.Unity.UIProviderSystem.View
{
    /// <summary>
    /// Реализация основы UI
    /// </summary>
    public partial class UserInterface
    {
        [SerializeField] private UIDragHandler dragZone;

        private CanvasScaler _canvasScaler;
        private CanvasScaler CanvasScaler => _canvasScaler ??= gameObject.GetComponentInParent<CanvasScaler>();

        private void SetPosition()
        {
            if (wndContainer == null || dragZone == null)
                return;
            if (CanvasScaler == null)
                return;
            wndContainer?.SetLocalPositionAndRotation(Offset, wndContainer.localRotation);
        }

        private void CalcPosition(Vector2 newPosition)
        {
            if (wndContainer == null || dragZone == null)
                return;
            if (CanvasScaler == null)
                return;
            var scale = CanvasScaler.transform.localScale;
            var h = Screen.height / (scale.y * 2f);
            var w = Screen.width / (scale.x * 2f);
            var min = wndContainer.rect.min;
            var max = wndContainer.rect.max;
            var position = Offset + new Vector2(newPosition.x / scale.x, newPosition.y / scale.y);
            if (position.x + min.x <= -w)
                position.x = -w - min.x;
            if (position.y + min.y <= -h)
                position.y = -h - min.y;
            if (position.x + max.x >= w)
                position.x = w - max.x;
            if (position.y + max.y >= h)
                position.y = h - max.y;
            position.x = Mathf.Floor(position.x);
            position.y = Mathf.Floor(position.y);
            Offset = position;
            wndContainer.SetLocalPositionAndRotation(Offset, wndContainer.localRotation);
        }
    }
}