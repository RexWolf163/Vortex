using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Unity.AudioSystem.Presets
{
    public partial class AudioChannelsConfig : ScriptableObject, ICoreAsset
    {
        [OnValueChanged("OnChannelsChanged", true)] [SerializeField]
        private string[] channels;

        public IReadOnlyList<string> GetChannels() => channels;

#if UNITY_EDITOR
        private void OnChannelsChanged()
        {
            AudioDriver.ResetChannels();
        }
#endif
    }
}