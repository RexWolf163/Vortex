using System;

namespace Vortex.Core.SettingsSystem.Model
{
    [Serializable]
    public class AssetCacheConfig
    {
        public AssetCacheConfig(int survivorCapacity)
        {
            SurvivorCapacity = survivorCapacity;
        }

        /// <summary>
        /// Размер LRU-буфера survivors. Ассеты без активных владельцев лежат здесь до переполнения,
        /// после чего самые старые реально выгружаются через Addressables.Release.
        /// </summary>
        public int SurvivorCapacity { get; }
    }
}