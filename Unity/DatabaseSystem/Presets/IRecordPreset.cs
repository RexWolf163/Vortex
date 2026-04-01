using System;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.DatabaseSystem.Model.Enums;

namespace Vortex.Unity.DatabaseSystem.Presets
{
    public interface IRecordPreset
    {
        /// <summary>
        /// Признак, определяющий как именно выдавать данные, в виде новой копии заполненной из пресета
        /// или ссылкой на класс в индексе 
        /// </summary>
        public RecordTypes RecordType { get; }

        /// <summary>
        /// Глобально уникальный идентификатор 
        /// </summary>
        public string GuidPreset { get; }

        /// <summary>
        /// Наименование элемента БД
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Возвращает модель данных заполненную из хранилища
        /// </summary>
        /// <returns></returns>
        public Record GetData();

        /// <summary>
        /// Проверка на соответствие типа контейнера данных и пресета
        /// </summary>
        /// <typeparam name="TU"></typeparam>
        /// <returns></returns>
        public bool CheckRecordType<TU>() where TU : Record;

        /// <summary>
        /// Проверка на соответствие типа контейнера данных и пресета
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool CheckRecordType(Type type);
    }
}