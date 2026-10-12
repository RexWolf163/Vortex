using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Focus
{
    /// <summary>
    /// Реактивная обёртка над текущим фокусом (<see cref="IFocusTarget"/> или <c>null</c>
    /// при отсутствии фокуса). Канон <see cref="ReactiveValue{T}"/>: equality по
    /// <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>, для reference-
    /// типа это ссылочное равенство — дедуп переходов «тот же target заново» бесплатно.
    /// </summary>
    public class FocusTargetData : ReactiveValue<IFocusTarget>
    {
        /// <summary>Без владельца: владелец назначается позже через <c>SetOwner</c>.</summary>
        public FocusTargetData(IFocusTarget value) => Value = value;

        /// <summary>Сразу закрытая на владельца: изменять значение может только он.</summary>
        public FocusTargetData(IFocusTarget value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}
