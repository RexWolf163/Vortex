namespace Vortex.Core.SettingsSystem.Model
{
    /// <summary>
    /// Partial-расширение <see cref="SettingsModel"/> от пакета AssetCacheSystem.
    /// Подключается к asmdef <c>ru.vortex.settings</c> через <c>ru.vortex.settings.ext.asmref</c>.
    /// <see cref="SettingsDriver"/> копирует поля из <c>AssetCacheSettings</c>-SO сюда по совпадению имён.
    /// </summary>
    public partial class SettingsModel
    {
        /// <summary>
        /// Флаг debug-трассировки: HIT/JOIN/LOAD/REL/SWEEP/EVICT в <c>Debug.Log</c>.
        /// Зеркало <c>DebugSettings.AssetCacheDebugLogs</c> — учитывает <c>DebugMode</c>-флаг.
        /// </summary>
        public bool AssetCacheDebugLogs { get; private set; }

        /// <summary>
        /// Параметры пакета AssetCacheSystem (capacity LRU survivors и т. п.).
        /// </summary>
        public AssetCacheConfig AssetCache { get; private set; }
    }
}
