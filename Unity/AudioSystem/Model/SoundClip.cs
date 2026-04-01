using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.AudioSystem.Model
{
    /// <summary>
    /// Звуковой клип
    /// Содержит диапазоны допустимых pitch и volume
    /// </summary>
    public class SoundClip
    {
        public AudioClip[] AudioClips { get; protected set; }
        public Vector2 PitchRange { get; }
        public Vector2 ValueRange { get; }

        /// <summary>
        /// Канал звука
        /// </summary>
        public AudioChannel Channel { get; protected set; }

        /// <summary>
        /// Зацикленность звука
        /// </summary>
        protected bool Loop;

        protected bool IsSingle = false;
        protected bool IsEmpty = false;

        public SoundClip(AudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop = false,
            string channelName = null)
        {
            AudioClips = audioClips;
            IsEmpty = AudioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips.Length == 1;

            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClip(AudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop = false,
            AudioChannel channel = null)
        {
            AudioClips = audioClips;
            IsEmpty = AudioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips.Length == 1;
            Channel = channel;
        }

        protected SoundClip()
        {
            IsEmpty = true;
        }

        public virtual float GetPitch() => Random.Range(PitchRange.x, PitchRange.y);
        public virtual float GetVolume() => Random.Range(ValueRange.x, ValueRange.y);
        public bool IsLoop() => Loop;

        public virtual AudioClip GetClip()
        {
            if (IsEmpty)
                return null;
            return IsSingle ? AudioClips[0] : AudioClips[Random.Range(0, AudioClips.Length)];
        }
    }
}