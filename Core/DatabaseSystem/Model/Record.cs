using Vortex.Core.System.Abstractions.SystemControllers;

namespace Vortex.Core.DatabaseSystem.Model
{
    public abstract partial class Record : SystemModel
    {
        /// <summary>
        /// Глобально уникальный идентификатор 
        /// </summary>
        public string GuidPreset { get; protected set; }

        /// <summary>
        /// Наименование элемента БД
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Описание элемента БД
        /// </summary>
        public string Description { get; protected set; }

        /// <summary>
        /// Получить данные под сохранение
        /// </summary>
        /// <returns></returns>
        public abstract string GetDataForSave();

        /// <summary>
        /// Восстановить из сохраненных данных
        /// </summary>
        /// <param name="data"></param>
        public abstract void LoadFromSaveData(string data);
    }
}