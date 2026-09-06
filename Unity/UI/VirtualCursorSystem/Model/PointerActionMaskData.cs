using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>Реактивная маска активных действий указателя.</summary>
    public class PointerActionMaskData : ReactiveValue<PointerActionMask>
    {
        public PointerActionMaskData(PointerActionMask value) => Value = value;

        public PointerActionMaskData(PointerActionMask value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}
