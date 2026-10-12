using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>Реактивный кадр жеста мультикасания.</summary>
    public class GestureFrameData : ReactiveValue<GestureFrame>
    {
        /// <summary>Без владельца: владелец назначается позже через <c>SetOwner</c>.</summary>
        public GestureFrameData(GestureFrame value) => Value = value;
    }
}
