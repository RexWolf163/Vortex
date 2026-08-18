using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Механика антиповтора (не выдавать недавно проигранные клипы). Задаётся на пресете.
        /// </summary>
        protected AntiRepeatMode AntiRepeat = AntiRepeatMode.LastMany;

        /// <summary>
        /// История недавно выбранных индексов для антиповтора (FIFO, размер = <c>N/2</c> округляя вниз).
        /// Живёт на инстансе <see cref="SoundClip"/>: запись Database (<c>Sound.Sample</c>) — стабильный инстанс,
        /// а <see cref="GetClip"/> зовётся на нём каждый плей → состояние переживает вызовы.
        /// </summary>
        private readonly List<int> _history = new();

        public SoundClip(AudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop = false,
            string channelName = null, AntiRepeatMode antiRepeat = AntiRepeatMode.LastMany)
        {
            AudioClips = audioClips;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            AntiRepeat = antiRepeat;
            IsSingle = audioClips == null || audioClips.Length == 1;

            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClip(AudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop = false,
            AudioChannel channel = null, AntiRepeatMode antiRepeat = AntiRepeatMode.LastMany)
        {
            AudioClips = audioClips;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            AntiRepeat = antiRepeat;
            IsSingle = audioClips == null || audioClips.Length == 1;
            Channel = channel;
        }

        protected SoundClip()
        {
            IsEmpty = true;
        }

#if ENABLE_ADDRESSABLES
        public SoundClip(AssetReferenceAudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange, bool loop,
            string channelName, AntiRepeatMode antiRepeat = AntiRepeatMode.LastMany)
        {
            _audioClipRefs = audioClips;
            AudioClips = null;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            AntiRepeat = antiRepeat;
            IsSingle = audioClips == null || audioClips.Length == 1;

            if (channelName.IsNullOrWhitespace()) return;
            var channel = AudioController.GetChannel(channelName);
            Channel = channel;
        }

        public SoundClip(AssetReferenceAudioClip[] audioClips, Vector2 pitchRange, Vector2 valueRange,
            bool loop = false,
            AudioChannel channel = null, AntiRepeatMode antiRepeat = AntiRepeatMode.LastMany)
        {
            _audioClipRefs = audioClips;
            IsEmpty = audioClips == null;
            PitchRange = pitchRange;
            ValueRange = valueRange;
            Loop = loop;
            AntiRepeat = antiRepeat;
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
            if (IsSingle)
                return AudioClips[0];

            var n = AudioClips.Length;

            if (AntiRepeat == AntiRepeatMode.None)
                return AudioClips[Random.Range(0, n)];

            // Размер истории по механике: с последним → 1; с последними → N/2 (округляя вниз). Оба < N (N>=2
            // здесь, т.к. N==1 отсекает IsSingle) → allowedCount = n − history.Count всегда >= 1.
            var historySize = AntiRepeat == AntiRepeatMode.LastOne ? 1 : n / 2;
            var allowedCount = n - _history.Count;
            var pick = Random.Range(0, allowedCount);

            var index = 0;
            for (var i = 0; i < n; i++)
            {
                if (_history.Contains(i))
                    continue;
                if (pick == 0)
                {
                    index = i;
                    break;
                }

                pick--;
            }

            _history.Add(index);
            while (_history.Count > historySize)
                _history.RemoveAt(0);

            return AudioClips[index];
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
                return new SoundClip(_audioClipRefs, PitchRange, ValueRange, Loop, channel: Channel,
                    antiRepeat: AntiRepeat);
#endif
            return new SoundClip(AudioClips, PitchRange, ValueRange, Loop, Channel, AntiRepeat);
        }
    }
}