using Vortex.Core.AudioSystem.Model;

namespace Vortex.Core.AudioSystem.Controllers
{
    /// <summary>
    /// Публичный фасад управления <see cref="AudioSampleWrapper"/>. Единственная точка смены
    /// <see cref="AudioSampleWrapper.State"/> и <see cref="AudioSampleWrapper.IsPaused"/> (через приватный
    /// <see cref="SetState"/>): держатель рулит только отсюда, сырые Play/Stop/Pause модели скрыты.
    /// </summary>
    public static class AudioSampleWrapperController
    {
        public static void Play(this AudioSampleWrapper wrapper)
        {
            if (wrapper.State == PlaybackState.Finished) // терминальное состояние — воскрешать нельзя
                return;
            // resume валиден только из паузы; а пауза (ниже) допускается лишь из Playing, поэтому Paused
            // всегда означает «реально играло и встало на паузу» → UnPause корректен, рестарта из Pending нет.
            var resume = wrapper.State == PlaybackState.Paused;
            SetState(wrapper, PlaybackState.Playing);
            wrapper.IsPaused = false;
            if (resume)
                wrapper.Resume();
            else
                wrapper.Play();
            wrapper.CallOnPlay();
        }

        public static void Pause(this AudioSampleWrapper wrapper)
        {
            if (wrapper.State != PlaybackState.Playing) // паузим только реально играющий (не Pending/Paused/Finished)
                return;
            SetState(wrapper, PlaybackState.Paused);
            wrapper.IsPaused = true;
            wrapper.Pause();
            wrapper.CallOnPaused();
        }

        public static void Stop(this AudioSampleWrapper wrapper)
        {
            if (wrapper.State == PlaybackState.Finished) // идемпотентно: не завершаем дважды (двойной OnFinished)
                return;
            SetState(wrapper, PlaybackState.Finished);
            wrapper.IsPaused = false;
            wrapper.Stop();
            wrapper.CallOnFinished();
            wrapper.Dispose();
        }

        /// <summary>
        /// Завершить хэндл без остановки источника (вытеснение). Аудио гасят плеер/фейд-твины, поэтому
        /// сам источник тут не трогаем — иначе на смене трека срежется fade-out.
        /// </summary>
        public static void Finish(this AudioSampleWrapper wrapper)
        {
            if (wrapper.State == PlaybackState.Finished) // идемпотентно: см. Stop
                return;
            SetState(wrapper, PlaybackState.Finished);
            wrapper.IsPaused = false;
            wrapper.CallOnFinished();
            wrapper.Dispose();
        }

        private static void SetState(AudioSampleWrapper wrapper, PlaybackState state) => wrapper.State = state;
    }
}
