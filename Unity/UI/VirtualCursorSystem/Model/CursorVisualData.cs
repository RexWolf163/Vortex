using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>Реактивный текущий вид курсора (резолв темы/hover/действий/разрешения).</summary>
    public class CursorVisualData : ReactiveValue<CursorVisual>
    {
        public CursorVisualData(CursorVisual value) => Value = value;

        public CursorVisualData(CursorVisual value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}
