using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Unity.AssetCacheSystem.Bus;

namespace Vortex.Unity.AssetCacheSystem
{
    /// <summary>
    /// Lazy-подгрузка ассета через Addressables. Работает поверх <see cref="AssetCache"/>:
    /// handle сам себе owner, dedup/LRU/sweep обеспечиваются кэшем.
    ///
    /// Живёт в пакете <c>AssetCacheSystem</c> (а не в отдельном), потому что это тонкий
    /// адаптер, полностью делегирующий работу в <c>AssetCache</c> — как <c>DirectAssetHandle</c>
    /// живёт рядом с <c>AssetHandle</c> в Core.
    ///
    /// Жизненный цикл:
    /// <list type="bullet">
    ///   <item>До первого <c>LoadAsync</c> — <c>Asset</c> = <c>null</c>, <c>IsLoaded</c> = <c>false</c>.</item>
    ///   <item>После успешного <c>LoadAsync</c> — <c>Asset</c> ≠ <c>null</c>, <c>IsLoaded</c> = <c>true</c>;
    ///   повторный вызов возвращает тот же экземпляр без обращения к кэшу.</item>
    ///   <item>После <c>Release</c> — handle в терминальном состоянии; последующий
    ///   <c>LoadAsync</c> — no-op с warning, возвращает <c>default(T)</c>.</item>
    /// </list>
    /// </summary>
    [Serializable]
    public sealed class AddressableAssetHandle<T> : AssetHandle<T> where T : UnityEngine.Object
    {
        private const string LoggerCategory = "AddressableAssetHandle";

        [SerializeField] private AssetReference reference;

        [NonSerialized] private T _cached;
        [NonSerialized] private bool _released;

        public override bool IsLoaded => _cached != null;
        public override T Asset => _cached;

        public override async UniTask<T> LoadAsync(CancellationToken ct)
        {
            if (_released)
            {
                Log.Print(LogLevel.Warning,
                    "LoadAsync called after Release — no-op, returning default. Consumer bug?",
                    LoggerCategory);
                return default;
            }

            if (_cached != null)
                return _cached;

            // AssetCache сам делает dedup: параллельные LoadAsync на одном handle резольвятся
            // через один UniTaskCompletionSource. Локальный _cached снимает даже это обращение
            // после первого успешного Load.
            _cached = await AssetCache.Load<T>(this, reference, ct);
            return _cached;
        }

        public override void Release()
        {
            if (_released) return;

            AssetCache.Release(this);
            _cached = null;
            _released = true;
        }
    }
}
