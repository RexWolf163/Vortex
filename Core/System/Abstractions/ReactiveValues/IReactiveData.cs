using System;

namespace Vortex.Core.System.Abstractions.ReactiveValues
{
    public interface IReactiveData
    {
        public event Action OnUpdateData;
    }
}