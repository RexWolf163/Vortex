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
    /// Жизненный цикл (handle переиспользуем — <c>Release</c> не терминален):
    /// <list type="bullet">
    ///   <item>До первого <c>LoadAsync</c> — <c>Asset</c> = <c>null</c>, <c>IsLoaded</c> = <c>false</c>.</item>
    ///   <item>После успешного <c>LoadAsync</c> — <c>Asset</c> ≠ <c>null</c>, <c>IsLoaded</c> = <c>true</c>;
    ///   повторный <c>LoadAsync</c> без <c>Release</c> возвращает тот же экземпляр и пишет warning
    ///   (загрузка поверх неотпущенного ассета — смелл жизненного цикла).</item>
    ///   <item>После <c>Release</c> — <c>_cached</c> = <c>null</c>; следующий <c>LoadAsync</c> грузит
    ///   ассет заново. Load и Release — забота держателя handle, по одному разу за цикл.</item>
    /// </list>
    /// </summary>
    [Serializable]
    public sealed class AddressableAssetHandle<T> : AssetHandle<T> where T : UnityEngine.Object
    {
        private const string LoggerCategory = "AddressableAssetHandle";

        [SerializeField] private AssetReference reference;

        [NonSerialized] private T _cached;

        public override bool IsLoaded => _cached != null;
        public override T Asset => _cached;

        public override async UniTask<T> LoadAsync(CancellationToken ct)
        {
            if (_cached != null)
            {
                // Загрузка поверх уже загруженного (не отпущенного) ассета — смелл жизненного цикла:
                // держатель забыл Release перед повторным Load либо грузит дважды. Отдаём тот же
                // экземпляр без повторного обращения к AssetCache, но сигналим.
                Log.Print(LogLevel.Warning,
                    "LoadAsync while the asset is already loaded (not released) — returning the same " +
                    "instance. A repeated load without a matching Release is a lifecycle smell.",
                    LoggerCategory);
                return _cached;
            }

            // AssetCache сам делает dedup параллельных LoadAsync на одном handle через общий
            // UniTaskCompletionSource; локальный _cached снимает даже это обращение после первого Load.
            _cached = await AssetCache.Load<T>(this, reference, ct);
            return _cached;
        }

        public override void Release()
        {
            // Нечего отпускать (свежий handle или уже отпущен) — идемпотентный no-op.
            if (_cached == null) return;

            AssetCache.Release(this);
            _cached = null;
        }
    }
}
