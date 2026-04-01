using System;

namespace Vortex.Core.DatabaseSystem
{
    public interface IDriverEditor
    {
        /// <summary>
        /// Возвращает пресет по GUID
        /// ВНИМАНИЕ! Применять только в редакторе!
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public Object GetPresetForRecord(string guid);

        /// <summary>
        /// Перезаполнить базу данных из пресетов
        /// ВНИМАНИЕ! Применять только в редакторе!
        /// </summary>
        public void ReloadDatabase();
    }
}