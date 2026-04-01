using System;

namespace Vortex.Core.System.Abstractions
{
    public interface IDataStorage
    {
        public event Action OnUpdateLink;

        public T GetData<T>() where T : class;
    }
}