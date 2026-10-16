using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
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
        /// <summary>
        /// Именованная группа кэша: ключ + собственная ёмкость survivors.
        /// </summary>
        [Serializable]
        private class GroupEntry
        {
            [Tooltip("Идентификатор группы. Передаётся в AssetCache.Load/Release и выбирается " +
                     "атрибутом [AssetCacheGroup].\nВторое поле - Размер LRU-буфера survivors группы. 0 — ассет выгружается сразу после Release.")]
            [HorizontalGroup, LabelText("Group")]
            public string key;

            [Min(0)] [HorizontalGroup, HideLabel] public int survivorCapacity;
        }

        [SerializeField, Min(0)]
        [Tooltip("Размер LRU-буфера survivors дефолтной группы (вызовы без группы). Ассеты без активных " +
                 "владельцев лежат здесь до переполнения, после чего самые старые выгружаются через " +
                 "Addressables.Release.")]
        private int survivorCapacity = 32;

        [SerializeField]
        [Tooltip("Именованные группы. У каждой свой реестр и своя ёмкость survivors — поток ассетов одной " +
                 "группы не вытесняет survivors другой.")]
        private GroupEntry[] groups = new GroupEntry[0];

        public AssetCacheConfig AssetCache => new(survivorCapacity,
            groups.Select(g => new AssetCacheGroupConfig(g.key, g.survivorCapacity)).ToArray());

        /// <summary>
        /// Ключи именованных групп в порядке настройки (для редакторных выпадашек).
        /// </summary>
        public IReadOnlyList<string> GetGroupKeys() => groups.Select(g => g.key).ToArray();
    }
}