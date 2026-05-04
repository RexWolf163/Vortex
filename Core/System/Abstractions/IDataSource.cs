using System;

namespace Vortex.Core.System.Abstractions
{
    /// <summary>
    /// Интерфейс хранилища множества данных
    ///
    /// Реализуется событие обновления части данных внутри хранилища
    /// </summary>
    public interface IDataSource
    {
        public event Action OnUpdateLink;
    }
}