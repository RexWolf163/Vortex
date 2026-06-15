#if ENABLE_ADDRESSABLES
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Vortex.Unity.AudioSystem.Model
{
    [Serializable]
    public class AssetReferenceAudioClip : AssetReferenceT<AudioClip>
    {
        public AssetReferenceAudioClip(string guid) : base(guid)
        {
        }
    }
}
#endif