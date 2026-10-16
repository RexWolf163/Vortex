using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Core.Extensions.LogicExtensions;
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
            // Ёмкости групп читаются один раз: подмена в рантайме не поддерживается —
            // изменение требует Cleanup + повторного Init.
            Model = new AssetCacheModel(new AssetCacheGroupModel(null, config.AssetCache.SurvivorCapacity));
            foreach (var group in config.AssetCache.Groups)
            {
                if (group.Key.IsNullOrWhitespace())
                {
                    Debug.LogError($"[{LogTag}] Group with empty key in AssetCacheSettings — skipped.");
                    continue;
                }

                if (Model.GetGroup(group.Key) != null)
                {
                    Debug.LogError($"[{LogTag}] Duplicate group «{group.Key}» in AssetCacheSettings — skipped.");
                    continue;
                }

                Model.AddGroup(new AssetCacheGroupModel(group.Key, group.SurvivorCapacity));
            }

            IsInitialized = true;
            if (_debugLogging)
                Debug.Log($"[{LogTag}] Initialized. SurvivorCapacity={Model.Default.SurvivorCapacity}, " +
                          $"groups={Model.GetGroups().Count - 1}");
            OnInitialized?.Invoke();
        }

        /// <inheritdoc/>
        public void Cleanup()
        {
            if (!IsInitialized) return;

            foreach (var group in Model.GetGroups())
            {
                // Прерываем все inflight: уведомляем waiter'ов отменой и освобождаем handle'ы.
                foreach (var pair in group.Inflight)
                {
                    pair.Value.Completion.TrySetCanceled();
                    if (pair.Value.Handle.IsValid())
                        Addressables.Release(pair.Value.Handle);
                }

                // Освобождаем все завершённые handle'ы (active + survivor — все в Handles).
                foreach (var pair in group.Handles)
                    if (pair.Value.IsValid())
                        Addressables.Release(pair.Value);

                group.Inflight.Clear();
                group.Handles.Clear();
                group.Locks.Clear();
                group.Survivors.Clear();
            }

            Model = null;

            IsInitialized = false;
            if (_debugLogging)
                Debug.Log($"[{LogTag}] Cleaned up.");
            OnReleased?.Invoke();
        }

        /// <summary>
        /// Группа для вызова. Пустой ключ — дефолтная. Неизвестный ключ — ошибка в лог и дефолтная группа:
        /// вызов не теряется, а мисконфиг виден (опечатка в ключе или группа удалена из настроек).
        /// </summary>
        private AssetCacheGroupModel ResolveGroup(string group)
        {
            var result = Model.GetGroup(group);
            if (result != null)
                return result;

            Debug.LogError($"[{LogTag}] Unknown group «{group}» — handled as default group. " +
                           "Check AssetCacheSettings.");
            return Model.Default;
        }

        private static string GroupLabel(AssetCacheGroupModel group) => group.Key ?? "<default>";
    }
}
