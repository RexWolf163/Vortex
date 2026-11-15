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
            SetTimer(Duration);
        }

        /// <summary>
        /// Таймер конца клипа: не-луп звук по нему завершается, зацикленный — сообщает о рестарте
        /// </summary>
        private void SetTimer(float delay)
        {
            delay = Mathf.Max(0f, delay);
            if (IsLoop)
                TimeController.Call(LoopRestart, delay, this);
            else
                TimeController.Call(AutoComplete, delay, this);
        }

        /// <summary>
        /// Остаток клипа в секундах: доля непроигранного (позиция источника в секундах клипа) ×
        /// <see cref="AudioSampleWrapper.Duration"/> (длительность уже с учётом pitch)
        /// </summary>
        private float GetRemaining() => Duration * (1f - _source.time / _clip.length);

        /// <summary>
        /// Естественный конец не-луп звука по таймеру: полное завершение ЧЕРЕЗ контроллер
        /// (State=Finished, OnFinished, Dispose), а не сырой инстансный Stop — иначе состояние и событие
        /// не выставятся. Явный вызов extension (this.Stop() ушёл бы в инстанс).
        /// </summary>
        private void AutoComplete() => AudioSampleWrapperController.Stop(this);

        /// <summary>
        /// Таймер зацикленного звука: клип пошёл на новый круг — событие рестарта. Таймер ставится заново
        /// на остаток по фактической позиции источника, поэтому ошибка от круга к кругу не копится.
        ///
        /// Сработать таймер может и чуть раньше перехода (очередь считает от времени начала кадра, источник
        /// стартует с задержкой вывода): источник ещё во второй половине клипа — событие не поднимаем и ждём
        /// остаток, иначе один переход дал бы два события.
        ///
        /// Таймер ставится до события: если подписчик остановит звук или поставит на паузу, он будет снят
        /// </summary>
        private void LoopRestart()
        {
            if (_source == null)
                return;
            var restarted = _source.time * 2f < _clip.length;
            SetTimer(GetRemaining());
            if (restarted)
                CallOnLoopRestart();
        }

        /// <summary>
        /// Снятие паузы. Pause снимает таймер конца клипа — ставим его заново на остаток. Без этого не-луп звук
        /// доигрывал, но хэндл не завершался и пул-элемент висел, а зацикленный перестал бы сообщать о рестартах
        /// </summary>
        protected override void Resume()
        {
            if (_source == null)
                return;
            _source.UnPause();
            SetTimer(GetRemaining());
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