using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.AudioSystem.Model
{
    /// <summary>
    /// Звуковой клип с зафиксированными значениями pitch и volume
    /// </summary>
    public class SoundClipFixed : SoundClip
    {
        /// <summary>
        /// Зафиксированное значение pitch
        /// </summary>
        private readonly float _currentPitch;

        /// <summary>
        /// Зафиксированное значение volume
        /// </summary>
        private readonly float _currentVolume;

        /// <summary>
        /// длительность клипа
        /// </summary>
        private readonly float _duration;

        /// <summary>
        /// Выбранный из выборки случайным образом аудиоклип
        /// </summary>
        public AudioClip AudioClip { get; protected set; }

        public SoundClipFixed(SoundClip clip, bool loop = false, string channelOverrideName = null)
        {
            AudioClip = clip.GetClip();
            _currentPitch = clip.GetPitch();
            _currentVolume = clip.GetVolume();
            _duration = _currentPitch == 0 ? float.MaxValue : AudioClip.length / Mathf.Abs(_currentPitch);
            Loop = loop;

            if (channelOverrideName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelOverrideName);
            Channel = channel;
        }

        public SoundClipFixed(SoundClip clip, bool loop = false, AudioChannel channelOverride = null)
        {
            AudioClip = clip.GetClip();
            _currentPitch = clip.GetPitch();
            _currentVolume = clip.GetVolume();
            _duration = _currentPitch == 0 ? float.MaxValue : AudioClip.length / Mathf.Abs(_currentPitch);
            Loop = loop;
            if (channelOverride != null)
                Channel = channelOverride;
        }

        public SoundClipFixed(AudioClip clip, bool loop = false, string channelName = null)
        {
            _currentPitch = 1f;
            _currentVolume = 1f;
            if (clip == null)
            {
                _duration = 0;
                return;
            }

            AudioClips = new[] { clip };
            IsEmpty = false;
            IsSingle = true;
            AudioClip = clip;
            _duration = clip.length;
            Loop = loop;
            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClipFixed(AudioClip clip, bool loop = false, AudioChannel channel = null)
        {
            _currentPitch = 1f;
            _currentVolume = 1f;
            if (clip == null)
            {
                _duration = 0;
                return;
            }

            AudioClips = new[] { clip };
            IsEmpty = false;
            IsSingle = true;
            AudioClip = clip;
            _duration = clip.length;
            Loop = loop;
            Channel = channel;
        }

        public override float GetPitch() => _currentPitch;
        public override float GetVolume() => _currentVolume;

        public float GetDuration() => _duration;
        public override AudioClip GetClip() => AudioClip;
    }
}