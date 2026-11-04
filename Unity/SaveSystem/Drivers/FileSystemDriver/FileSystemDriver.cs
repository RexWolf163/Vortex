using System;
using System.Collections.Generic;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    /// <summary>
    /// Драйвер сохранения в файлы на диске.
    /// Корневой путь — FileBus.GetAppPath()/{папка из SaveSettings}/ (по умолчанию Saves).
    /// На каждый сейв пишет два файла:
    ///   - {guid}.save    — XML SavePreset с данными, сжатый через Compress(guid).
    ///   - {guid}.summary — XML SaveSummary (для быстрого индекса без чтения тела сейва).
    /// Дополнительно — файл .in в корне Saves/ хранит инкремент-счётчик последнего сейва.
    /// </summary>
    public sealed partial class FileSystemDriver : Singleton<FileSystemDriver>, IDriver
    {
        private const string SaveExtension = ".save";
        private const string SummaryExtension = ".summary";
        private const string IncrementFile = ".in";

        /// <summary>
        /// Локальный реестр существующих сейвов: guid → SaveSummary.
        /// Заполняется при Init сканированием папки Saves/, обновляется при Save/Remove.
        /// </summary>
        private static readonly Dictionary<string, SaveSummary> Saves = new();

        /// <summary>
        /// Линк на индекс данных, передаётся SaveController через SetIndexLink.
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> _saveDataIndex;

        /// <summary>
        /// Номер-инкремент последнего сейва. -1 = ещё не вычислен.
        /// </summary>
        private int _increment = -1;

        /// <summary>
        /// Сводка текущего цикла сохранения: заводится в <see cref="BeginSummary"/>, копит метаблоки из
        /// <see cref="AddMeta"/>, дописывается и записывается в <see cref="Save"/>.
        /// </summary>
        private SaveSummary _summary;

        public event Action OnInit;

        public void SetIndexLink(Dictionary<string, Dictionary<string, string>> index) => _saveDataIndex = index;

        public void BeginSummary() => _summary = new SaveSummary { Meta = new List<SaveMeta>() };

        public void AddMeta(string key, string metadata)
        {
            if (SaveSettings.IsMetaEnabled(key))
                _summary.SetMeta(key, metadata);
        }

        public void Destroy()
        {
            // Ничего не держит. Файлы остаются на диске.
        }
    }
}
