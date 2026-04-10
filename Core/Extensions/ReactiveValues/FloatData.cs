namespace Vortex.Core.Extensions.ReactiveValues
{
    public class FloatData : ReactiveValue<float>
    {
        public FloatData(float value) => Value = value;
        public FloatData(float value, object owner)
        {
            Value = value;
            _owner = owner;
        }

    }
}