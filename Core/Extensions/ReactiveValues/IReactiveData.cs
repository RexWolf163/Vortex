using System;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Core.Extensions.ReactiveValues
{
    [POCO]
    public interface IReactiveData
    {
        public event Action OnUpdateData;
    }
}