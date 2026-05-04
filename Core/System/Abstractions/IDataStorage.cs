namespace Vortex.Core.System.Abstractions
{
    /// <summary>
    /// Интерфейс хранилища уникальных конкретизированных данных
    ///
    /// Реализует метод поиска возвращающий данные из внутреннего реестра(или хранилища)
    /// по ожидаемому типу
    /// </summary>
    public interface IDataStorage : IDataSource
    {
        public T GetData<T>() where T : class;
    }
}