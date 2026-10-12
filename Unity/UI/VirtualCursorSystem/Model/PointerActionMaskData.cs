using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>Реактивная маска активных действий указателя.</summary>
    public class PointerActionMaskData : ReactiveValue<PointerActionMask>
    {
        /// <summary>Без владельца: владелец назначается позже через <c>SetOwner</c>.</summary>
        public PointerActionMaskData(PointerActionMask value) => Value = value;

        /// <summary>Сразу закрытая на владельца: изменять значение может только он.</summary>
        public PointerActionMaskData(PointerActionMask value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}
