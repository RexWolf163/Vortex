using UnityEngine;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Пишет <c>PointerModel.IsOverUI</c> из UGUI EventSystem (замена UITK-picking). Потребители мировой
    /// проекции гейтят клик по этому флагу. Использует no-arg <see cref="EventSystem.IsPointerOverGameObject()"/>
    /// (последний указатель) — при нескольких указателях, возможно, потребуется передавать pointerId
    /// виртуального устройства.
    /// </summary>
    public class IsOverUiHandler : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (VirtualCursorBus.Data == null || EventSystem.current == null)
                return;
            VirtualCursorController.SetOverUI(EventSystem.current.IsPointerOverGameObject());
        }
    }
}
