using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;

namespace Vortex.Unity.AssetCacheSystem.Models
{
    /// <summary>
    /// Runtime-реестр пакета AssetCacheSystem: набор изолированных групп кэша.
    ///
    /// <see cref="Default"/> — группа без идентификатора, с ней работают все вызовы без группы.
    /// Именованные группы — из <c>AssetCacheConfig.Groups</c>. Устройство реестра группы —
    /// см. <see cref="AssetCacheGroupModel"/>.
    ///
    /// Модель НЕ реактивна: потребители получают результат напрямую через
    /// <c>await AssetCache.Load(...)</c>, никаких подписок на изменения кэша не предусмотрено.
    /// </summary>
    public sealed class AssetCacheModel
    {
        internal AssetCacheModel(AssetCacheGroupModel defaultGroup)
        {
            Default = defaultGroup;
            AllGroups.Add(defaultGroup);
        }

        /// <summary>Дефолтная группа (без идентификатора).</summary>
        public AssetCacheGroupModel Default { get; }

        /// <summary>Именованные группы по ключу. Дефолтной здесь нет.</summary>
        internal Dictionary<string, AssetCacheGroupModel> Groups { get; } = new();

        /// <summary>Все группы: дефолтная первой, далее именованные в порядке настройки.</summary>
        internal List<AssetCacheGroupModel> AllGroups { get; } = new();

        /// <summary>Все группы: дефолтная первой, далее именованные в порядке настройки.</summary>
        public IReadOnlyList<AssetCacheGroupModel> GetGroups() => AllGroups;

        /// <summary>
        /// Группа по ключу. Пустой ключ — <see cref="Default"/>; неизвестный — <c>null</c>.
        /// </summary>
        public AssetCacheGroupModel GetGroup(string key) =>
            string.IsNullOrEmpty(key) ? Default : Groups.GetValueOrDefault(key);

        internal void AddGroup(AssetCacheGroupModel group)
        {
            Groups.Add(group.Key, group);
            AllGroups.Add(group);
        }

        /// <summary>Возвращает true, если ассет загружен хотя бы в одной группе (active или survivor).</summary>
        public bool IsLoaded(AssetReference reference) => AllGroups.Any(g => g.IsLoaded(reference));

        /// <summary>Количество загруженных handle'ов по всем группам (active + survivors).</summary>
        public int LoadedCount => AllGroups.Sum(g => g.LoadedCount);

        /// <summary>Количество незавершённых загрузок по всем группам.</summary>
        public int InflightCount => AllGroups.Sum(g => g.InflightCount);

        /// <summary>Количество ассетов в survivor-буферах всех групп.</summary>
        public int SurvivorsCount => AllGroups.Sum(g => g.SurvivorsCount);

        /// <summary>Количество уникальных владельцев по всем группам.</summary>
        public int OwnersCount => AllGroups.SelectMany(g => g.Locks.Keys).Distinct().Count();
    }
}
