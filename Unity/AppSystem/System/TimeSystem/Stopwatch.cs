using System;

namespace Vortex.Unity.AppSystem.System.TimeSystem
{
    /// <summary>
    /// Секундомер: считает время ВВЕРХ от старта до явного <see cref="Stop"/> — зеркало
    /// <see cref="Timer"/> (тот считает вниз к дедлайну). Опросный, как <see cref="Timer"/> и
    /// <c>DateTimeTimer</c>: реактивных полей нет намеренно — значение читает UI нужного секундомера,
    /// а не все активные тикают постоянно ради тех, на кого никто не смотрит. Конструктор отсчёт НЕ
    /// запускает.
    ///
    /// Два режима запуска: <see cref="Start"/> — паузируемый (<see cref="SetPause"/>/<see cref="Resume"/>
    /// и пауза редактора замораживают накопление, меряется активное игровое время без пауз);
    /// <see cref="StartRealtime"/> — пауза-агностик (чистый wall-clock от старта, включая паузы).
    /// </summary>
    public class Stopwatch : IDisposable
    {
        private DateTime _since;    // момент последнего старта/резюма
        private TimeSpan _elapsed;  // накопленное — заморожено на паузе и после Stop
        private bool _running;
        private bool _pausable;
        private bool _disposed;
        private bool _editorPaused;

        /// <summary>Накопленное время. Растёт, пока секундомер идёт; заморожено на паузе и после <see cref="Stop"/>.</summary>
        public TimeSpan Elapsed => _running ? _elapsed + (DateTime.UtcNow - _since) : _elapsed;

        /// <summary>Целые секунды <see cref="Elapsed"/> (усечение) — для опроса из UI без работы с TimeSpan.</summary>
        public int TotalSeconds => (int)Elapsed.TotalSeconds;

        /// <summary>Идёт ли отсчёт (запущен, не на паузе, не остановлен).</summary>
        public bool IsRunning => _running;

        /// <summary>На паузе: запущен паузируемым и заморожен <see cref="SetPause"/> или паузой редактора.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Конструктор НЕ запускает отсчёт — нужен явный <see cref="Start"/>/<see cref="StartRealtime"/>.</summary>
        public Stopwatch() { }

        /// <summary>
        /// Запустить паузируемый секундомер: <see cref="SetPause"/>/<see cref="Resume"/> и пауза
        /// редактора замораживают накопление, поэтому меряется активное игровое время (без пауз).
        /// Старт с нуля; повторный старт активного или стоящего на паузе — no-op.
        /// </summary>
        public void Start() => StartInternal(pausable: true);

        /// <summary>
        /// Запустить пауза-агностичный секундомер: чистый wall-clock от старта, паузу не слушает
        /// (<see cref="SetPause"/> и пауза редактора игнорируются) — меряется полное реальное время,
        /// включая паузы. Старт с нуля; повторный старт активного — no-op.
        /// </summary>
        public void StartRealtime() => StartInternal(pausable: false);

        private void StartInternal(bool pausable)
        {
            if (_disposed || _running || IsPaused) return;

            _pausable = pausable;
            _elapsed = TimeSpan.Zero;
            _since = DateTime.UtcNow;
            _running = true;

            if (_pausable)
                HookEditorPause();
        }

        /// <summary>
        /// Заморозить накопление. No-op для непаузируемого режима (<see cref="StartRealtime"/>), для
        /// уже стоящего на паузе, остановленного или после <see cref="Dispose"/>.
        /// </summary>
        public void SetPause()
        {
            if (!_running || !_pausable) return;

            _elapsed += DateTime.UtcNow - _since;
            _running = false;
            IsPaused = true;
        }

        /// <summary>Возобновить после <see cref="SetPause"/>. No-op, если не на паузе.</summary>
        public void Resume()
        {
            if (!IsPaused) return;

            _since = DateTime.UtcNow;
            _running = true;
            IsPaused = false;
        }

        /// <summary>
        /// Остановить: фиксирует <see cref="Elapsed"/>, дальше значение не растёт. Терминально —
        /// возобновлению не подлежит (для нового замера — <see cref="Reset"/> + <see cref="Start"/>,
        /// либо просто <see cref="Start"/>, он и так начинает с нуля).
        /// </summary>
        public void Stop()
        {
            if (_running)
                _elapsed += DateTime.UtcNow - _since;

            _running = false;
            IsPaused = false;
            UnhookEditorPause();
        }

        /// <summary>Обнулить и остановить — секундомер готов к новому <see cref="Start"/>/<see cref="StartRealtime"/>.</summary>
        public void Reset()
        {
            _elapsed = TimeSpan.Zero;
            _running = false;
            IsPaused = false;
            UnhookEditorPause();
        }

        /// <summary>
        /// Полная очистка: снимает отписку от паузы редактора, отсчёт мёртв. Идемпотентно; после
        /// <see cref="Dispose"/> старт невозможен.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _running = false;
            IsPaused = false;
            UnhookEditorPause();
        }

        public override string ToString() =>
            $"Stopwatch: {Elapsed.TotalSeconds}s ({(_running ? "running" : IsPaused ? "paused" : "stopped")})";

#if UNITY_EDITOR
        // Пауза редактора (⏸) реальное время не тормозит, а Elapsed считается по DateTime.UtcNow —
        // иначе паузируемый секундомер «съел» бы весь простой редактора. Замораживаем его на editor-
        // паузу так же, как на паузе по стейту. Только для паузируемого режима (StartRealtime не хукает).
        private void HookEditorPause() => UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
        private void UnhookEditorPause() => UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPause;

        private void OnEditorPause(UnityEditor.PauseState state)
        {
            if (state == UnityEditor.PauseState.Paused)
            {
                if (!_running || IsPaused) return;   // уже на паузе (стейт) / не идёт — не трогаем
                _editorPaused = true;
                SetPause();
            }
            else
            {
                if (!_editorPaused) return;           // не мы ставили паузу — не воскрешаем
                _editorPaused = false;
                Resume();
            }
        }
#else
        private void HookEditorPause() { }
        private void UnhookEditorPause() { }
#endif
    }
}
