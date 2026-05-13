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
        /// Handle от Addressables. Хранится в untyped-форме — реальный тип resolves'я
        /// на стороне waiter'а через generic-параметр <c>Load&lt;T&gt;</c>.
        /// </summary>
        public AsyncOperationHandle<UnityEngine.Object> Handle;

        /// <summary>
        /// Broadcast-результат для всех waiter'ов.
        /// </summary>
        public UniTaskCompletionSource<UnityEngine.Object> Completion;
    }
}
