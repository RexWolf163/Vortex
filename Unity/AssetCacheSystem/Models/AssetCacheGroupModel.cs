using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Vortex.Unity.AssetCacheSystem.Models
{
    /// <summary>
    /// Реестр одной группы кэша.
    ///
    /// Содержит четыре связанные структуры:
    /// - <see cref="Locks"/> — кто (owner) какие <see cref="AssetReference"/> держит и сколькими удержаниями.
    /// - <see cref="Handles"/> — Addressables-handle на каждый загруженный ассет (один handle = один ref).
    /// - <see cref="Inflight"/> — текущие незавершённые загрузки для дедупликации параллельных запросов.
    /// - <see cref="Survivors"/> — FIFO LRU-буфер ассетов без активных владельцев, ждущих eviction.
    ///
    /// Группы изолированы: один и тот же ref, загруженный в двух группах, держит в каждой свой handle.
    /// Физическую выгрузку это не ломает — Addressables ведёт общий счётчик ссылок по ключу,
    /// и ассет уходит из памяти только когда handle'ы отпустили все группы.
    /// </summary>
    public sealed class AssetCacheGroupModel
    {
        internal AssetCacheGroupModel(string key, int survivorCapacity)
        {
            Key = key;
            SurvivorCapacity = survivorCapacity;
        }

        /// <summary>Идентификатор группы. <c>null</c> — дефолтная группа.</summary>
        public string Key { get; }

        /// <summary>Размер LRU-буфера survivors группы.</summary>
        public int SurvivorCapacity { get; }

        /// <summary>Дефолтная ли это группа (без идентификатора).</summary>
        public bool IsDefault => Key == null;

        /// <summary>
        /// Кто что держит. Ключ — owner, значение — счётчик удержаний по каждому AssetReference:
        /// каждый Load добавляет одно удержание, отмена/провал этого Load снимает ровно его,
        /// Release владельца — все разом.
        /// </summary>
        internal Dictionary<object, Dictionary<AssetReference, int>> Locks { get; } = new();

        /// <summary>Загруженные handle'ы по AssetReference. Один handle на каждый ref.</summary>
        internal Dictionary<AssetReference, AsyncOperationHandle> Handles { get; } = new();

        /// <summary>Текущие незавершённые загрузки. Снимается из словаря после завершения.</summary>
        internal Dictionary<AssetReference, InflightLoad> Inflight { get; } = new();

        /// <summary>FIFO-список survivors: голова — самый старый, хвост — самый новый.</summary>
        internal LinkedList<AssetReference> Survivors { get; } = new();

        /// <summary>Возвращает true, если ассет загружен в группе (active или survivor).</summary>
        public bool IsLoaded(AssetReference reference) => Handles.ContainsKey(reference);

        /// <summary>Количество загруженных ассетов группы (active + survivors).</summary>
        public int LoadedCount => Handles.Count;

        /// <summary>Количество незавершённых загрузок группы.</summary>
        public int InflightCount => Inflight.Count;

        /// <summary>Количество ассетов в survivor LRU-буфере группы.</summary>
        public int SurvivorsCount => Survivors.Count;

        /// <summary>Количество владельцев, зарегистрированных в группе.</summary>
        public int OwnersCount => Locks.Count;
    }
}
