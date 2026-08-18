using UnityEngine;
using Vortex.Core.AudioSystem.Model;

namespace Vortex.Unity.AudioSystem.Model
{
    public class MusicWrapper : AudioSampleWrapper
    {
        private AudioSource _source;

        public MusicWrapper(AudioSource source, AudioClip clip)
        {
            _source = source;
            IsLoop = true;
            Duration = clip != null ? clip.length : 0f;
        }

        /// <summary>
        /// Отвязка хэндла от источника. Реальный стоп аудио — задача плеера/фейд-твинов, поэтому Dispose
        /// НЕ дёргает <c>source.Stop()</c> (иначе на смене трека срежется fade-out).
        /// </summary>
        public override void Dispose() => _source = null;

        protected override void Play()
        {
            if (_source == null)
                return;
            _source.Play();
        }

        protected override void Resume()
        {
            if (_source == null)
                return;
            _source.UnPause();
        }

        protected override void Stop()
        {
            if (_source == null)
                return;
            _source.Stop();
        }

        protected override void Pause()
        {
            if (_source == null)
                return;
            _source.Pause();
        }
    }
}
