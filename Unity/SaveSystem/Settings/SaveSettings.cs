using System.Collections.Generic;
using System.Reflection;
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
    ///
    /// Partial: переключатели метаблоков сводки объявляют пакеты-владельцы метаблоков в своих файлах
    /// (asmref в <c>ru.vortex.unity.settings</c>) — bool-поле с <see cref="SaveMetaBlockAttribute"/> в группе
    /// <see cref="MetaGroup"/>, по умолчанию <c>true</c>. Кнопку On/Off добавляет
    /// <c>SaveSettingsMetaBlockProcessor</c> (Editor).
    /// </summary>
    public partial class SaveSettings : SettingsPreset
    {
        private const string SettingsPath = "Settings";

        /// <summary>
        /// Группа инспектора переключателей метаблоков
        /// </summary>
        private const string MetaGroup = "Метаданные сейва";

        /// <summary>
        /// Порядок группы <see cref="MetaGroup"/> — после остальных групп
        /// </summary>
        private const float MetaGroupOrder = 100;

        private static readonly SaveReactor[] Empty = new SaveReactor[0];

        private static SaveSettings _instance;

        /// <summary>
        /// Переключатели метаблоков по ключу: собираются с полей <see cref="SaveMetaBlockAttribute"/> один раз.
        /// </summary>
        private static Dictionary<string, FieldInfo> _metaFields;

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

        [BoxGroup(MetaGroup, order: MetaGroupOrder), PropertyOrder(-1)]
        [InfoBox("Метаблоки сводки сейва (превью, маркеры). Выключенный блок драйвер не записывает. Кнопки " +
                 "добавляют пакеты-владельцы метаблоков; блок без кнопки пишется всегда.")]
        [OnInspectorGUI]
        private void MetaBlocksInfo()
        {
        }

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
            var instance = GetInstance();
            return instance != null ? instance.reactors : Empty;
        }

        /// <summary>
        /// Включён ли метаблок с ключом <paramref name="key"/>. Для ключа не объявлено переключателя или нет
        /// ассета настроек — включён.
        /// </summary>
        public static bool IsMetaEnabled(string key)
        {
            var instance = GetInstance();
            if (instance == null)
                return true;

            return !GetMetaFields().TryGetValue(key, out var field) || (bool)field.GetValue(instance);
        }

        private static Dictionary<string, FieldInfo> GetMetaFields()
        {
            if (_metaFields != null)
                return _metaFields;

            _metaFields = new Dictionary<string, FieldInfo>();
            foreach (var field in typeof(SaveSettings).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attr = field.GetCustomAttribute<SaveMetaBlockAttribute>();
                if (attr == null)
                    continue;

                if (field.FieldType != typeof(bool))
                {
                    Debug.LogError(
                        $"[SaveSettings] {nameof(SaveMetaBlockAttribute)} на не-bool поле \"{field.Name}\" — пропущено.");
                    continue;
                }

                if (!_metaFields.TryAdd(attr.Key, field))
                    Debug.LogError(
                        $"[SaveSettings] Дубликат ключа метаблока \"{attr.Key}\" на поле \"{field.Name}\" — пропущено.");
            }

            return _metaFields;
        }

        /// <summary>
        /// Единственный ассет настроек из <c>Resources/Settings</c>; null — ассета нет.
        /// </summary>
        private static SaveSettings GetInstance()
        {
            if (_instance != null)
                return _instance;

            var assets = Resources.LoadAll<SaveSettings>(SettingsPath);
            if (assets != null && assets.Length > 0)
                _instance = assets[0];
            return _instance;
        }
    }
}
