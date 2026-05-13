using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        public bool IsInitialized { get; private set; }
        public AssetCacheModel Model { get; private set; }

        public event Action OnInitialized;
        public event Action OnReleased;

        /// <summary>
        /// Кэш конфигурации, считанной из <c>Settings.Data().AssetCache</c> на <see cref="Init"/>.
        /// </summary>
        private int _survivorCapacity;

        private bool _debugLogging;

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