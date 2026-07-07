using System;
using UnityEngine;

namespace Vortex.Unity.AppSystem.System.TimeSystem
{
    /// <summary>
    /// Класс таймера, для простой интеграции и управления
    /// отложенным запуском логики.
    /// Для ситуаций когда нужно осуществлять управление длительностью используя,
    /// в том числе, режим паузы
    /// </summary>
    public class Timer : IDisposable
    {
        public DateTime End { get; protected set; }
        public TimeSpan Duration { get; protected set; }

        /// <summary>
        /// Сколько времени осталось.
        /// </summary>
        public TimeSpan Remains
        {
            get
            {
                if (IsPaused)
                    return _remains;
                _remains = IsComplete ? TimeSpan.Zero : End - DateTime.UtcNow;
                if (!IsComplete && _remains <= TimeSpan.Zero)
                    _remains = TimeSpan.Zero;

                return _remains;
            }
            protected set => _remains = value;
        }

        /// <summary>
        /// Остаток таймера
        /// </summary>
        private TimeSpan _remains;

        /// <summary>
        /// Таймер завершен
        /// </summary>
        public bool IsComplete { get; protected set; }

        /// <summary>
        /// Поставлен ли таймер на паузу
        /// </summary>
        public bool IsPaused { get; protected set; }

        private Action _onRunOut;
        private bool _disposed;

        public Timer(DateTime end, Action onRunOut)
        {
            End = end;
            Duration = End - DateTime.UtcNow;
            IsComplete = false;
            _remains = TimeSpan.Zero;
            _onRunOut = onRunOut;
            TimeController.Call(CallAction, (float)Duration.TotalSeconds, this);
            HookEditorPause();
        }

        public Timer(TimeSpan duration, Action onRunOut)
        {
            End = DateTime.UtcNow.Add(duration);
            Duration = duration;
            IsComplete = false;
            _remains = TimeSpan.Zero;
            _onRunOut = onRunOut;
            TimeController.Call(CallAction, (float)Duration.TotalSeconds, this);
            HookEditorPause();
        }

        public Timer(float duration, Action onRunOut)
        {
            Duration = TimeSpan.FromSeconds(duration);
            End = DateTime.UtcNow.Add(Duration);
            IsComplete = false;
            _remains = TimeSpan.Zero;
            _onRunOut = onRunOut;
            TimeController.Call(CallAction, (float)Duration.TotalSeconds, this);
            HookEditorPause();
        }

        /// <summary>
        /// Приостановить таймер
        /// </summary>
        public void SetPause()
        {
            if (IsPaused || IsComplete) return;
            TimeController.RemoveCall(this);
            _ = Remains;
            IsPaused = true;
        }

        /// <summary>
        /// Запустить после паузы 
        /// </summary>
        public void Resume()
        {
            if (!IsPaused || IsComplete) return;
            End = DateTime.UtcNow.Add(Remains);
            IsPaused = false;
            TimeController.Call(CallAction, (float)Remains.TotalSeconds, this);
        }

        /// <summary>
        /// Сколько времени прошло
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetTimePassed()
        {
            if (IsComplete)
                return Duration;
            return Duration - Remains;
        }

        public override string ToString() => $"Timer: remains {Remains.TotalSeconds} from {Duration.TotalSeconds}s";

        private void CallAction()
        {
            IsComplete = true;
            UnhookEditorPause();
            _onRunOut?.Invoke();
        }

        /// <summary>
        /// Полная остановка и очистка: снимает отложенный вызов и отписку от паузы редактора.
        /// В отличие от <see cref="SetPause"/> (пауза — таймер жив и может воскреснуть на снятии
        /// паузы редактора), после Dispose таймер мёртв и собирается GC. Идемпотентно.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsComplete = true;               // не активен: SetPause/Resume/CallAction — no-op
            TimeController.RemoveCall(this);  // снять отложенный вызов
            UnhookEditorPause();             // рвём подписку на pauseStateChanged (иначе висит в GC / воскреснет)
            _onRunOut = null;
        }

#if UNITY_EDITOR
        // Пауза редактора (кнопка ⏸) реальное время не тормозит, а Timer считает по
        // DateTime.UtcNow — иначе при снятии паузы он «съест» весь простой. Гоним его в
        // паузу так же, как на Application Pause.
        private void HookEditorPause() => UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
        private void UnhookEditorPause() => UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPause;

        private void OnEditorPause(UnityEditor.PauseState state)
        {
            if (state == UnityEditor.PauseState.Paused)
                SetPause();
            else
                Resume();
        }
#else
        private void HookEditorPause() { }
        private void UnhookEditorPause() { }
#endif
    }
}