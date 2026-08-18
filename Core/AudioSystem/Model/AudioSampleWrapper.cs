using System;
using Vortex.Core.Extensions.LogicExtensions.Actions;

namespace Vortex.Core.AudioSystem.Model
{
    /// <summary>
    /// Обертка управления для вызываемого звука.
    /// Предоставляет держателю доступ к событиям звука
    /// и возможность ограниченного контроля 
    /// </summary>
    public abstract class AudioSampleWrapper : IDisposable
    {
        public event Action OnPlay;
        public event Action OnFinished;
        public event Action OnPaused;

        public bool IsLoop { get; protected set; }
        public bool IsPaused { get; internal set; }
        public float Duration { get; protected set; }

        /// <summary>Состояние воспроизведения. Меняет только контроллер (<c>AudioSampleWrapperController</c>).</summary>
        public PlaybackState State { get; internal set; }

        public abstract void Dispose();
        protected internal abstract void Play();
        protected internal abstract void Resume();
        protected internal abstract void Stop();
        protected internal abstract void Pause();

        internal void CallOnPaused() => OnPaused.Fire();
        internal void CallOnPlay() => OnPlay.Fire();
        internal void CallOnFinished() => OnFinished.Fire();
    }
}