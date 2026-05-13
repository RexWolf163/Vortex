using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AssetCacheSystem.Abstractions;
using Vortex.Unity.AssetCacheSystem.Models;

namespace Vortex.Unity.AssetCacheSystem.Controllers
{
    /// <summary>
    /// Singleton-контроллер пакета AssetCacheSystem.
    /// Lifecycle: Init / Cleanup идемпотентны и вызываются шиной <c>AssetCache</c>.
    /// </summary>
    public sealed partial class AssetCacheController
        : Singleton<AssetCacheController>, IAssetCacheController
    {
        private const string LogTag = "AssetCache";

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public AssetCacheModel Model { get; private set; }

        /// <inheritdoc/>
        public event Action OnInitialized;

        /// <inheritdoc/>
        public event Action OnReleased;

        /// <summary>
        /// Кэш capacity LRU-буфера survivors, считанный из <c>Settings.Data().AssetCache</c>
        /// в <see cref="Init"/>. Подмена в рантайме не поддерживается — изменение требует
        /// <see cref="Cleanup"/> + повторного <see cref="Init"/>.
        /// </summary>
        private int _survivorCapacity;

        /// <summary>
        /// Включает <see cref="Debug.Log"/>-трассировку HIT/JOIN/LOAD/REL/SWEEP/EVICT.
        /// Читается из <c>Settings.Data().AssetCacheDebugLogs</c> в <see cref="Init"/>.
        /// </summary>
        private bool _debugLogging;

        /// <inheritdoc/>
        public void Init()
        {
            if (IsInitialized) return;

            var config = Settings.Data();
            if (config == null)
            {
                Debug.LogError($"[{LogTag}] Settings.Data().AssetCache is null. " +
                               $"Убедись, что в Resources/Settings/ присутствует AssetCacheSettings-asset.");
                return;
            }

            _debugLogging = config.AssetCacheDebugLogs;
            _survivorCapacity = config.AssetCache.SurvivorCapacity;
            Model = new AssetCacheModel();

            IsInitialized = true;
            if (_debugLogging)
                Debug.Log($"[{LogTag}] Initialized. SurvivorCapacity={_survivorCapacity}");
            OnInitialized?.Invoke();
        }

        /// <inheritdoc/>
        public void Cleanup()
        {
            if (!IsInitialized) return;

            // Прерываем все inflight: уведомляем waiter'ов отменой и освобождаем handle'ы.
            foreach (var pair in Model.Inflight)
            {
                pair.Value.Completion.TrySetCanceled();
                if (pair.Value.Handle.IsValid())
                    Addressables.Release(pair.Value.Handle);
            }

            // Освобождаем все завершённые handle'ы (active + survivor — все в Model.Handles).
            foreach (var pair in Model.Handles)
                if (pair.Value.IsValid())
                    Addressables.Release(pair.Value);

            Model.Inflight.Clear();
            Model.Handles.Clear();
            Model.Locks.Clear();
            Model.Survivors.Clear();
            Model = null;

            IsInitialized = false;
            if (_debugLogging)
                Debug.Log($"[{LogTag}] Cleaned up.");
            OnReleased?.Invoke();
        }
    }
}