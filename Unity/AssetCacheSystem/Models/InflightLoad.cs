using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Vortex.Unity.AssetCacheSystem.Models
{
    /// <summary>
    /// Незавершённая загрузка одного ассета.
    /// Параллельные запросы того же AssetReference подключаются к существующему inflight
    /// и получают результат через <see cref="Completion"/> — без второго вызова
    /// <c>Addressables.LoadAssetAsync</c>.
    /// </summary>
    internal sealed class InflightLoad
    {
        /// <summary>
        /// Handle от Addressables. Хранится в untyped (негенерик) форме — реальный тип resolve'ится
        /// на стороне waiter'а через generic-параметр <c>Load&lt;T&gt;</c> (типизированная загрузка
        /// <c>LoadAssetAsync&lt;T&gt;</c> неявно приводится к негенерик-хэндлу).
        /// </summary>
        public AsyncOperationHandle Handle;

        /// <summary>
        /// Broadcast-результат для всех waiter'ов.
        /// </summary>
        public UniTaskCompletionSource<UnityEngine.Object> Completion;
    }
}
