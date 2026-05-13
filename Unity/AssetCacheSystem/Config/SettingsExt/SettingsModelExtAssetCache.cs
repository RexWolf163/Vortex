namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public bool AssetCacheDebugLogs { get; private set; }

        public AssetCacheConfig AssetCache { get; private set; }
    }
}