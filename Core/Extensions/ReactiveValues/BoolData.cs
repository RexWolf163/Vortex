namespace Vortex.Core.Extensions.ReactiveValues
{
    public class BoolData : ReactiveValue<bool>
    {
        public BoolData(bool value) => Value = value;

        public BoolData(bool value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}