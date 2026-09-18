using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.SaveSystem.Reactors;
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
        private const string SettingsPath = "Settings";

        private static readonly SaveReactor[] Empty = new SaveReactor[0];

        private static SaveSettings _instance;

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

        [BoxGroup("Коррекция устаревших сейвов")]
        [InfoBox("Блоки коррекции применяются по порядку при загрузке слота: каждому задано окно версий сборки, " +
                 "которой записан сейв. Правка идёт по распакованной строке до разбора, сам файл не переписывается. " +
                 "Пусто — коррекции нет.")]
        [SerializeReference]
        private SaveReactor[] reactors = new SaveReactor[0];

        public string SavesFolder => savesFolder;

        public int GlobalSaveBackups => globalSaveBackups;

        public string GlobalSaveFolder => globalSaveFolder;

        /// <summary>
        /// Блоки коррекции в порядке применения. Читают драйверы слотов напрямую из пресета: в
        /// <c>SettingsModel</c> список не переносится — расширение модели живёт в сборке настроек, а она о
        /// SaveSystem не знает.
        /// </summary>
        public IReadOnlyList<SaveReactor> Reactors => reactors;

        /// <summary>
        /// Список коррекций из единственного ассета настроек. Ассета нет — пустой список: загрузка идёт без
        /// коррекции, как до появления механизма.
        /// </summary>
        public static IReadOnlyList<SaveReactor> GetReactors()
        {
            if (_instance != null)
                return _instance.reactors;

            var assets = Resources.LoadAll<SaveSettings>(SettingsPath);
            if (assets == null || assets.Length == 0)
                return Empty;

            _instance = assets[0];
            return _instance.reactors;
        }
    }
}
