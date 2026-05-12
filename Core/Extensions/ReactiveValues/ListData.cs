using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.Extensions.ReactiveValues
{
    public class ListData<T> : ReactiveCollection<T>
    {
        public ListData() => Value = new List<T>();

        public ListData(List<T> value)
        {
            Value = value.ToList();
        }
    }
}