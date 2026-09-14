namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        /// <summary>Папка сейвов относительно корня данных приложения (файловый драйвер слотов).</summary>
        public string SavesFolder { get; private set; }

        /// <summary>Сколько резервных копий глобального хранилища держать. 0 — копий нет.</summary>
        public int GlobalSaveBackups { get; private set; }

        /// <summary>Папка файла глобального хранилища относительно корня данных приложения (файловый драйвер).</summary>
        public string GlobalSaveFolder { get; private set; }

        /// <summary>
        /// Ошибка чтения глобального хранилища или дубль ключа модуля останавливают загрузку. Только в редакторе:
        /// в билде всегда <c>false</c>.
        /// </summary>
        public bool GlobalSaveFailFast { get; private set; }
    }
}
