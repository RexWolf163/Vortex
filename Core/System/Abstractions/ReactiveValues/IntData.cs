namespace Vortex.Core.System.Abstractions.ReactiveValues
{
    public class IntData : ReactiveValue<int>
    {
        public IntData(int value) => Value = value;
    }
}