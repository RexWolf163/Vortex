#if ENABLE_ADDRESSABLES
using System;
using Spine.Unity;
using UnityEngine.AddressableAssets;

namespace Vortex.SpineExtensions.Addressable
{
    [Serializable]
    public class AssetReferenceSkeletonDataAsset : AssetReferenceT<SkeletonDataAsset>
    {
        public AssetReferenceSkeletonDataAsset(string guid) : base(guid)
        {
        }
    }
}
#endif