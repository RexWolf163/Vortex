using System;
using UnityEngine;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.AudioSystem.Model
{
    public class SoundWrapper : AudioSampleWrapper
    {
        private AudioSource _source;
        private AudioClip _clip;

        private Action _stopCallback;

        public SoundWrapper(AudioClip clip, float duration, bool loop = false)
        {
            _clip = clip;
            IsLoop = loop;
            Duration = duration;
        }

        /// <summary>
        /// Подключение к реальному источнику. Зовёт <c>AudioSourceHandler</c> пулового элемента, когда находит
        /// этот хэндл в своих данных — до этого управление (Play/Stop/Pause) — no-op.
        /// </summary>
        public void Init(AudioSource source, Action stopCallback)
        {
            _source = source;
            _source.loop = IsLoop;
            _source.clip = _clip;
            _stopCallback = stopCallback;
        }

        /// <summary>Отвязка от источника. Реальный стоп — за источником/хэндлером.</summary>
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
            if (IsLoop) return;
            TimeController.Call(AutoComplete, Duration, this);
        }

        /// <summary>
        /// Естественный конец не-луп звука по таймеру: полное завершение ЧЕРЕЗ контроллер
        /// (State=Finished, OnFinished, Dispose), а не сырой инстансный Stop — иначе состояние и событие
        /// не выставятся. Явный вызов extension (this.Stop() ушёл бы в инстанс).
        /// </summary>
        private void AutoComplete() => AudioSampleWrapperController.Stop(this);

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
            TimeController.RemoveCall(this);
            _source.Stop();
            _stopCallback?.Invoke();
        }

        protected override void Pause()
        {
            if (_source == null)
                return;
            TimeController.RemoveCall(this);
            _source.Pause();
        }
    }
}