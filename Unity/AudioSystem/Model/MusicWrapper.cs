using UnityEngine;
using Vortex.Core.AudioSystem.Model;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.AudioSystem.Model
{
    public class MusicWrapper : AudioSampleWrapper
    {
        private AudioSource _source;

        /// <param name="source">Источник плеера</param>
        /// <param name="duration">
        /// Длительность круга с учётом pitch. Считает клип (<see cref="SoundClipFixed.GetDuration"/>) —
        /// так же, как для <see cref="SoundWrapper"/>
        /// </param>
        public MusicWrapper(AudioSource source, float duration)
        {
            _source = source;
            IsLoop = true;
            Duration = duration;
        }

        /// <summary>
        /// Отвязка хэндла от источника. Реальный стоп аудио — задача плеера/фейд-твинов, поэтому Dispose
        /// НЕ дёргает <c>source.Stop()</c> (иначе на смене трека срежется fade-out).
        /// </summary>
        public override void Dispose()
        {
            TimeController.RemoveCall(this);
            _source = null;
        }

        protected override void Play()
        {
            if (_source == null)
                return;
            _source.Play();
            SetTimer();
        }

        protected override void Resume()
        {
            if (_source == null)
                return;
            _source.UnPause();
            SetTimer();
        }

        protected override void Stop()
        {
            if (_source == null)
                return;
            TimeController.RemoveCall(this);
            _source.Stop();
        }

        protected override void Pause()
        {
            if (_source == null)
                return;
            TimeController.RemoveCall(this);
            _source.Pause();
        }

        /// <summary>
        /// Таймер рестарта на остаток клипа — по фактической позиции и pitch источника
        /// </summary>
        private void SetTimer()
        {
            var clip = _source.clip;
            if (clip == null)
                return;
            //При нулевом pitch клип стоит на месте — круг не закончится
            var pitch = Mathf.Abs(_source.pitch);
            if (pitch <= 0f)
                return;
            TimeController.Call(LoopRestart, (clip.length - _source.time) / pitch, this);
        }

        /// <summary>
        /// Музыка зациклена всегда: клип пошёл на новый круг — событие рестарта. Таймер ставится заново
        /// на остаток по фактической позиции источника, поэтому ошибка от круга к кругу не копится.
        ///
        /// Сработать таймер может и чуть раньше перехода (очередь считает от времени начала кадра, источник
        /// стартует с задержкой вывода): источник ещё во второй половине клипа — событие не поднимаем и ждём
        /// остаток, иначе один переход дал бы два события.
        ///
        /// Таймер ставится до события: если подписчик остановит музыку или поставит на паузу, он будет снят
        /// </summary>
        private void LoopRestart()
        {
            if (_source == null)
                return;
            var clip = _source.clip;
            var restarted = clip != null && _source.time * 2f < clip.length;
            SetTimer();
            if (restarted)
                CallOnLoopRestart();
        }
    }
}
