namespace Vortex.Core.System.Abstractions.ReactiveValues
{
    public class BoolData : ReactiveValue<bool>
    {
        public BoolData(bool value) => Value = value;
    }
}