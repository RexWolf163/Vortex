using System;
using UnityEngine;
using Vortex.Core.SettingsSystem.Model;
using Vortex.Unity.CoreAssetsSystem;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.AssetCacheSystem.Config
{
    /// <summary>
    /// Project-level настройки пакета AssetCacheSystem.
    /// Один экземпляр в Resources/Settings/ — подхватывается <c>SettingsDriver</c> через рефлексию
    /// по совпадению имени свойства <see cref="AssetCache"/> с одноимённым свойством на <see cref="SettingsModel"/>.
    /// </summary>
    [Serializable]
    public sealed class AssetCacheSettings : SettingsPreset, ICoreAsset
    {
        [SerializeField, Min(0)]
        [Tooltip("Размер LRU-буфера survivors. Ассеты без активных владельцев лежат здесь до переполнения, " +
                 "после чего самые старые выгружаются через Addressables.Release.")]
        private int survivorCapacity = 32;

        public AssetCacheConfig AssetCache => new(survivorCapacity);
    }
}