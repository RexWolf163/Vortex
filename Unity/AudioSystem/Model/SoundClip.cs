using System;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Random = UnityEngine.Random;

namespace Vortex.Unity.AudioSystem.Model
{
    /// <summary>
    /// Звуковой клип
    /// Содержит диапазоны допустимых pitch и volume
    /// </summary>
    public class SoundClip : ICloneable
    {
        public AudioClip[] AudioClips { get; protected set; }

#if ENABLE_ADDRESSABLES
        private readonly AssetReferenceAudioClip[] _audioClipRefs;
#endif

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
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips == null || audioClips.Length == 1;

            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClip(AudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop = false,
            AudioChannel channel = null)
        {
            AudioClips = audioClips;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips == null || audioClips.Length == 1;
            Channel = channel;
        }

        protected SoundClip()
        {
            IsEmpty = true;
        }

#if ENABLE_ADDRESSABLES
        public SoundClip(AssetReferenceAudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop,
            string channelName)
        {
            _audioClipRefs = audioClips;
            AudioClips = null;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips == null || audioClips.Length == 1;

            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClip(AssetReferenceAudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange,
            bool loop = false,
            AudioChannel channel = null)
        {
            _audioClipRefs = audioClips;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            IsSingle = audioClips == null || audioClips.Length == 1;
            Channel = channel;
        }

#endif
        public virtual float GetPitch() => Random.Range(PitchRange.x, PitchRange.y);
        public virtual float GetVolume() => Random.Range(ValueRange.x, ValueRange.y);
        public bool IsLoop() => Loop;

        public virtual AudioClip GetClip()
        {
            if (IsEmpty)
                return null;
#if ENABLE_ADDRESSABLES
            if (_audioClipRefs != null && AudioClips == null)
                LoadAssets();
#endif
            return IsSingle ? AudioClips[0] : AudioClips[Random.Range(0, AudioClips.Length)];
        }

#if ENABLE_ADDRESSABLES
        /// <summary>
        /// Подгрузка addressable данных.
        /// Важно! Политика подразумевает что все загруженное - будет висеть до смерти приложения.
        /// Иное привело бы к размытию контракта Database и перегрузке счетчиками и обработками
        /// во всех случаях ее применения, а не только в адрессабл вариантах
        /// </summary>
        private void LoadAssets()
        {
            AudioClips = new AudioClip[_audioClipRefs.Length];
            for (var i = 0; i < _audioClipRefs.Length; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    AudioClips[i] = _audioClipRefs[i].editorAsset;
                    continue;
                }
#endif

                var handle = _audioClipRefs[i].LoadAssetAsync<AudioClip>();
                AudioClips[i] = handle.WaitForCompletion(); // синхронно, валидный клип, без дедлока
            }
        }
#endif

        /// <summary>
        /// Deep Clone
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
#if ENABLE_ADDRESSABLES
            if (_audioClipRefs != null && AudioClips == null)
                return new SoundClip(_audioClipRefs, PitchRange, ValueRange, Loop, channel: Channel);
#endif
            return new SoundClip(AudioClips, PitchRange, ValueRange, Loop, Channel);
        }
    }
}