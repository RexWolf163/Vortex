using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    /// <summary>
    /// Partial-расширение <see cref="DebugSettings"/> от пакета AssetCacheSystem.
    /// Подключается к asmdef <c>ru.vortex.unity.debug</c> через <c>ru.vortex.unity.debug.asmref</c>.
    /// Добавляет один toggle для трассировки операций кэша; учитывается только при включённом
    /// глобальном <c>DebugMode</c>.
    /// </summary>
    public partial class DebugSettings
    {
        [BoxGroup("Log Settings")] [SerializeField] [ToggleButton(isSingleButton: true)]
        private bool assetCacheDebugLogs;

        /// <summary>
        /// Трассировка операций <c>AssetCache</c> (HIT/JOIN/LOAD/REL/SWEEP/EVICT) в <c>Debug.Log</c>.
        /// Возвращает <c>true</c> только если активны и <see cref="DebugSettings"/>.DebugMode,
        /// и локальный toggle.
        /// </summary>
        public bool AssetCacheDebugLogs => DebugMode && assetCacheDebugLogs;
    }
}
