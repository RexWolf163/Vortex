using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>Реактивный текущий вид курсора (резолв темы/hover/действий/разрешения).</summary>
    public class CursorVisualData : ReactiveValue<CursorData>
    {
        public CursorVisualData(CursorData value) => Value = value;

        public CursorVisualData(CursorData value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}
