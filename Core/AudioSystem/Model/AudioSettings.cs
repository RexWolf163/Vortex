using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Core.AudioSystem.Model
{
    public class AudioSettings
    {
        public float MasterVolume { get; internal set; } = 1;
        public float SoundVolume { get; internal set; } = 1;
        public float MusicVolume { get; internal set; } = 1;

        public bool MasterOn { get; internal set; } = true;
        public bool SoundOn { get; internal set; } = true;
        public bool MusicOn { get; internal set; } = true;

        public Dictionary<string, AudioChannel> Channels { get; } = IndexFabric.Create<AudioChannel>();
    }
}