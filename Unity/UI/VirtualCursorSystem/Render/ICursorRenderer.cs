using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Рендерер курсора: применяет render-агностичный <see cref="CursorVisual"/> в позиции курсора.
    /// Реализации сами подписываются на <c>VirtualCursorBus.Visual</c> (+ ScreenPosition для UI-рендера).
    /// </summary>
    public interface ICursorRenderer
    {
        void Apply(in CursorVisual visual, Vector2 screenPosition);
    }
}
