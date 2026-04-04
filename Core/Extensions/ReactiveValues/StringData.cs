namespace Vortex.Core.Extensions.ReactiveValues
{
    public class StringData : ReactiveValue<string>
    {
        public StringData(string value) => Value = value;

        public override string ToString() => Value;
    }
}