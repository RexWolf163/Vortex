using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.SaveSystem.Presets
{
    /// <summary>
    /// Настройки системы сохранений: слоты (<c>SaveController</c>) и глобальное хранилище
    /// (<c>GlobalSaveController</c>). Пресет настроек: значения копируются в <c>SettingsModel</c>, откуда их
    /// читают Core-контроллеры и драйверы. Папки задаются относительно корня данных приложения
    /// (<c>FileBus.GetAppPath()</c>) и используются файловыми драйверами; пусто — корень.
    /// </summary>
    public class SaveSettings : SettingsPreset
    {
        [BoxGroup("Слоты сохранения")]
        [SerializeField, Tooltip("Папка сейвов относительно корня данных приложения (файловый драйвер). Пусто — корень.")]
        private string savesFolder = "Saves";

        [BoxGroup("Глобальное хранилище")]
        [InfoBox("Резервные копии: одна копия за запуск, при завершении приложения; при переполнении удаляется " +
                 "самая старая. Нечитаемый основной контейнер поднимается из самой свежей целой копии. " +
                 "0 — копий нет: испорченный контейнер не восстанавливается.")]
        [SerializeField, MinValue(0)]
        private int globalSaveBackups;

        [BoxGroup("Глобальное хранилище")]
        [SerializeField, Tooltip("Папка файла глобального хранилища и его копий относительно корня данных " +
                                 "приложения (файловый драйвер). Пусто — корень.")]
        private string globalSaveFolder = "Global";

        public string SavesFolder => savesFolder;

        public int GlobalSaveBackups => globalSaveBackups;

        public string GlobalSaveFolder => globalSaveFolder;
    }
}
