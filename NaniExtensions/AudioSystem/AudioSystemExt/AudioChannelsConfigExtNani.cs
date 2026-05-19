#if USING_NANINOVELL
using UnityEngine;
using Vortex.Unity.AudioSystem.Attributes;

namespace Vortex.Unity.AudioSystem.Presets
{
    public partial class AudioChannelsConfig
    {
        [SerializeField, AudioChannelName] private string naniBgmChannel;
        [SerializeField, AudioChannelName] private string naniSfxChannel;
        [SerializeField, AudioChannelName] private string naniVoiceChannel;
        [SerializeField, AudioChannelName] private string naniCutsceneVoiceChannel;

        public string GetNaniBgmChannel() => naniBgmChannel;
        public string GetSfxChannel() => naniSfxChannel;
        public string GetVoiceChannel() => naniVoiceChannel;
        public string GetVoiceCutsceneChannel() => naniCutsceneVoiceChannel;
    }
}
#endif