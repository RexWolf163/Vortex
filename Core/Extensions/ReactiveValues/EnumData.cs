using System;

namespace Vortex.Core.Extensions.ReactiveValues
{
    public class EnumData<T> : ReactiveValue<T> where T : Enum
    {
        public EnumData(T value) => Value = value;

        public EnumData(T value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}