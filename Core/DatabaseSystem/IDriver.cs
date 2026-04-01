using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.DatabaseSystem
{
    public interface IDriver : ISystemDriver
    {
        /// <summary>
        /// Передача указателя на реестр БД в драйвер для заполнения
        /// </summary>
        /// <param name="singletonRecords">линк на индекс синглтон-моделей</param>
        /// <param name="uniqRecords">линк на список id по которым могут быть представлены
        /// модели заполненные из пресета</param>
        public void SetIndex(Dictionary<string, Record> singletonRecords, HashSet<string> uniqRecords);

        /// <summary>
        /// Создает из пресета и возвращает новый экземпляр Record 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public T GetNewRecord<T>(string guid) where T : Record, new();

        /// <summary>
        /// Возвращает новые экземпляры для всех multyinstance пресетов в БД
        /// чьи модели отвечают указанному типу
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] GetNewRecords<T>() where T : Record, new();

        /// <summary>
        /// Проверяет соответствие пресета указанному типу
        /// </summary>
        /// <param name="guid"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool CheckPresetType<T>(string guid) where T : Record;

        /*
        /// <summary>
        /// Возвращает тип системы к которой относится
        /// </summary>
        /// <returns></returns>
        Type GetOwnerSystem();
    */
    }
}