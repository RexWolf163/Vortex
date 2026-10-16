using System;
using System.Collections.Generic;

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
        /// <param name="groups">См. <see cref="Groups"/>. <c>null</c> — именованных групп нет.</param>
        public AssetCacheConfig(int survivorCapacity, IReadOnlyList<AssetCacheGroupConfig> groups = null)
        {
            SurvivorCapacity = survivorCapacity;
            Groups = groups ?? new AssetCacheGroupConfig[0];
        }

        /// <summary>
        /// Размер LRU-буфера survivors дефолтной группы (группы без идентификатора).
        /// Ассеты без активных владельцев лежат здесь до переполнения,
        /// после чего самые старые реально выгружаются через <c>Addressables.Release</c>.
        /// </summary>
        public int SurvivorCapacity { get; }

        /// <summary>
        /// Именованные группы кэша. У каждой — свой реестр и своя ёмкость survivors.
        /// Дефолтная группа сюда не входит — её ёмкость в <see cref="SurvivorCapacity"/>.
        /// </summary>
        public IReadOnlyList<AssetCacheGroupConfig> Groups { get; }
    }

    /// <summary>
    /// Immutable настройка именованной группы кэша.
    /// </summary>
    [Serializable]
    public class AssetCacheGroupConfig
    {
        public AssetCacheGroupConfig(string key, int survivorCapacity)
        {
            Key = key;
            SurvivorCapacity = survivorCapacity;
        }

        /// <summary>Идентификатор группы — передаётся в <c>Load</c>/<c>Release</c>.</summary>
        public string Key { get; }

        /// <summary>Размер LRU-буфера survivors группы.</summary>
        public int SurvivorCapacity { get; }
    }
}
