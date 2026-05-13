using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.SettingsSystem.Model;
using Vortex.Unity.AssetCacheSystem.Abstractions;
using Vortex.Unity.AssetCacheSystem.Controllers;
using Vortex.Unity.AssetCacheSystem.Models;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Bus
{
    /// <summary>
    /// Статическая шина пакета AssetCacheSystem.
    ///
    /// При загрузке домена через <see cref="RuntimeInitializeOnLoadMethodAttribute"/> подписывается
    /// на <c>Settings.OnInit</c> (с InitValve-семантикой: если Settings уже инициализированы,
    /// вызов происходит сразу) и создаёт <see cref="AssetCacheController"/> через Singleton-доступ.
    ///
    /// Public API из двух consumer-методов: <see cref="Load{T}"/> и <see cref="Release"/>.
    /// </summary>
    public static class AssetCache
    {
        /// <summary>Активный контроллер пакета.</summary>
        public static IAssetCacheController Controller { get; private set; }

        /// <summary>Конфигурация пакета (считана из Settings.Data().AssetCache).</summary>
        public static AssetCacheConfig Config { get; private set; }

        /// <summary>Runtime-модель пакета (для отладки/инспекции, мутация не поддерживается).</summary>
        public static AssetCacheModel Data => Controller?.Model;

        /// <summary>Контроллер инициализирован и готов к работе.</summary>
        public static bool IsReady => Controller is { IsInitialized: true };

        /// <summary>
        /// Контроллер инициализирован. InitValve — подписки, сделанные ПОСЛЕ инициализации,
        /// выполняются немедленно.
        /// </summary>
        public static InitValve OnReady { get; } = InitValve.Create(out OnInitComplete);

        private static readonly Action OnInitComplete;

        /// <summary>Контроллер очищен (Cleanup).</summary>
        public static event Action OnRelease;

        /// <summary>
        /// Запрос ассета. См. <see cref="IAssetCacheController.Load{T}"/>.
        /// </summary>
        public static UniTask<T> Load<T>(object owner, AssetReference reference,
            CancellationToken ct = default) where T : Object
            => Controller.Load<T>(owner, reference, ct);

        /// <summary>
        /// Освобождение всех ассетов владельца. См. <see cref="IAssetCacheController.Release"/>.
        /// </summary>
        public static void Release(object owner) => Controller?.Release(owner);

        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            Settings.OnInit -= CreateController;
            Settings.OnInit += CreateController;

            App.OnExit -= Dispose;
            App.OnExit += Dispose;
        }

        private static void Dispose()
        {
            Settings.OnInit -= CreateController;
            App.OnExit -= Dispose;
            Controller?.Cleanup();
        }

        private static void CreateController()
        {
            if (Controller != null) return;

            var settingsData = Settings.Data();
            if (settingsData == null) return;

            Config = settingsData.AssetCache;
            if (Config == null)
            {
                Debug.LogError("[AssetCache] Settings.Data().AssetCache is null. " +
                               "Убедись, что в Resources/Settings/ присутствует AssetCacheSettings-asset.");
                return;
            }

            Controller = AssetCacheController.Instance;
            Controller.OnInitialized -= NotifyReady;
            Controller.OnInitialized += NotifyReady;
            Controller.OnReleased -= NotifyReleased;
            Controller.OnReleased += NotifyReleased;

            Controller.Init();
        }

        private static void NotifyReady() => OnInitComplete?.Invoke(); //открытие клапана

        private static void NotifyReleased()
        {
            Controller.OnInitialized -= NotifyReady;
            Controller.OnReleased -= NotifyReleased;
            OnRelease?.Invoke();
        }
    }
}