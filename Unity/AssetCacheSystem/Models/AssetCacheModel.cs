using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Vortex.Unity.AssetCacheSystem.Models
{
    /// <summary>
    /// Runtime-реестр пакета AssetCacheSystem.
    ///
    /// Содержит четыре связанные структуры:
    /// - <see cref="Locks"/> — кто (owner) какие <see cref="AssetReference"/> держит.
    /// - <see cref="Handles"/> — Addressables-handle на каждый загруженный ассет (один handle = один ref).
    /// - <see cref="Inflight"/> — текущие незавершённые загрузки для дедупликации параллельных запросов.
    /// - <see cref="Survivors"/> — FIFO LRU-буфер ассетов без активных владельцев, ждущих eviction.
    ///
    /// Модель НЕ реактивна: потребители получают результат напрямую через
    /// <c>await AssetCache.Load(...)</c>, никаких подписок на изменения кэша не предусмотрено.
    /// </summary>
    public sealed class AssetCacheModel
    {
        /// <summary>Кто что держит. Ключ — owner, значение — набор удерживаемых AssetReference.</summary>
        internal Dictionary<object, HashSet<AssetReference>> Locks { get; } = new();

        /// <summary>Загруженные handle'ы по AssetReference. Один handle на каждый ref.</summary>
        internal Dictionary<AssetReference, AsyncOperationHandle> Handles { get; } = new();

        /// <summary>Текущие незавершённые загрузки. Снимается из словаря после завершения.</summary>
        internal Dictionary<AssetReference, InflightLoad> Inflight { get; } = new();

        /// <summary>FIFO-список survivors: голова — самый старый, хвост — самый новый.</summary>
        internal LinkedList<AssetReference> Survivors { get; } = new();

        /// <summary>Возвращает true, если ассет загружен и присутствует в кэше (active или survivor).</summary>
        public bool IsLoaded(AssetReference reference) => Handles.ContainsKey(reference);

        /// <summary>Количество загруженных ассетов (active + survivors).</summary>
        public int LoadedCount => Handles.Count;

        /// <summary>Количество текущих незавершённых загрузок.</summary>
        public int InflightCount => Inflight.Count;

        /// <summary>Количество ассетов в survivor LRU-буфере.</summary>
        public int SurvivorsCount => Survivors.Count;

        /// <summary>Количество зарегистрированных владельцев.</summary>
        public int OwnersCount => Locks.Count;
    }
}
