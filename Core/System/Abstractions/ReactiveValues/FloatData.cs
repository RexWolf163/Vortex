namespace Vortex.Core.System.Abstractions.ReactiveValues
{
    public class FloatData : ReactiveValue<float>
    {
        public FloatData(float value) => Value = value;
    }
}