using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vortex.Core.AssetHandleSystem
{
    /// <summary>
    /// Прямая ссылка на Unity-ассет. Ассет находится в памяти всегда, пока живёт содержащий
    /// его пресет. Compat-реализация для проектов без Addressables и для лёгких/всегда нужных
    /// ассетов, где lazy-load избыточен.
    /// </summary>
    [Serializable]
    public sealed class DirectAssetHandle<T> : AssetHandle<T> where T : UnityEngine.Object
    {
        [SerializeField] private T asset;

        public override bool IsLoaded => asset != null;
        public override T Asset => asset;

        /// <summary>Мгновенно возвращает завёрнутую ссылку. <paramref name="ct"/> игнорируется.</summary>
        public override UniTask<T> LoadAsync(CancellationToken ct) => UniTask.FromResult(asset);

        /// <summary>No-op — прямую ссылку освобождать нечего, ассет держится сериализованным полем.</summary>
        public override void Release()
        {
        }
    }
}
