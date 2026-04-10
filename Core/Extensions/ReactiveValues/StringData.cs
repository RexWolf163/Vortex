namespace Vortex.Core.Extensions.ReactiveValues
{
    public class StringData : ReactiveValue<string>
    {
        public StringData(string value) => Value = value;

        public StringData(string value, object owner)
        {
            Value = value;
            _owner = owner;
        }


        public override string ToString() => Value;
    }
}