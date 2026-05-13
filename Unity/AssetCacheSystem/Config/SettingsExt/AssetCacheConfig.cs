using System;

namespace Vortex.Core.SettingsSystem.Model
{
    /// <summary>
    /// Immutable конфигурация пакета AssetCacheSystem.
    /// Создаётся в <c>AssetCacheSettings.AssetCache</c>-property и копируется в
    /// <c>SettingsModel.AssetCache</c> через рефлексию <c>SettingsDriver</c>'а.
    /// Все runtime-параметры контроллера читаются отсюда один раз в <c>AssetCacheController.Init</c>.
    /// </summary>
    [Serializable]
    public class AssetCacheConfig
    {
        /// <param name="survivorCapacity">См. <see cref="SurvivorCapacity"/>.</param>
        public AssetCacheConfig(int survivorCapacity)
        {
            SurvivorCapacity = survivorCapacity;
        }

        /// <summary>
        /// Размер LRU-буфера survivors. Ассеты без активных владельцев лежат здесь до переполнения,
        /// после чего самые старые реально выгружаются через <c>Addressables.Release</c>.
        /// </summary>
        public int SurvivorCapacity { get; }
    }
}
