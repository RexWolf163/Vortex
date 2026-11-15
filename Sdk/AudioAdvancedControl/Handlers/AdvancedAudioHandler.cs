using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Sdk.AudioAdvancedControl.Model;
using Vortex.Sdk.Core.GameCore;
using Random = UnityEngine.Random;

namespace Vortex.Sdk.AudioAdvancedControl.Handlers
{
    /// <summary>
    /// Общая часть хэндлеров управляемого выгружаемого звука: список клипов за <see cref="AssetHandle{T}"/>,
    /// выбор трека по режиму, загрузка клипа перед запуском и отпуск после останова, пауза.
    ///
    /// Жизнь звука привязана к объекту: на <c>OnEnable</c> — автозапуск (если включён), на <c>OnDisable</c> —
    /// останов и отпуск клипа. Хэндлер удерживает клип играющего трека и, пока грузится, — запрошенного;
    /// отпущенный клип остаётся в буфере кэша ассетов и выгружается по его правилам.
    ///
    /// Запрос трека: клип асинхронно запрашивается у хэндла. У каждого запроса свой токен отмены:
    /// новый запрос и останов отменяют прежний, ещё не исполненный, — до запуска он не дойдёт.
    ///
    /// Конец трека хэндлер сам не отсчитывает — его сообщает аудиосистема (пауза в её сигналах уже учтена):
    /// зацикленный клип пошёл на новый круг (<see cref="AudioSampleWrapper.OnLoopRestart"/>) либо незацикленный
    /// доиграл (<see cref="AudioSampleWrapper.OnFinished"/>). В режимах <see cref="AdvancedPlaybackMode.IndexOneShot"/>
    /// и <see cref="AdvancedPlaybackMode.RandomOneShot"/> трек после этого останавливается, в
    /// <see cref="AdvancedPlaybackMode.Index"/> и <see cref="AdvancedPlaybackMode.Random"/> запускается следующий.
    ///
    /// Пауз две, независимых: пауза игры (<see cref="GameStates.Paused"/>) и ручная (<see cref="Pause"/> /
    /// <see cref="Unpause"/>). Звук играет, только когда нет ни одной.
    ///
    /// Публичные методы рассчитаны на вызов из состояний <c>UIStateSwitcher</c> (<c>EventFire</c>).
    /// Куда именно идёт звук, решает наследник
    /// </summary>
    public abstract class AdvancedAudioHandler : MonoBehaviour
    {
        [SerializeReference, Tooltip("Клипы. В памяти держится клип играющего трека и, пока грузится, запрошенного.")]
        private AssetHandle<AudioClip>[] clips = new AssetHandle<AudioClip>[0];

        [SerializeField, Tooltip(
             "Index — после окончания трека следующий по списку; Random — после окончания случайный; " +
             "IndexOneShot — следующий по списку один раз; RandomOneShot — случайный один раз.")]
        private AdvancedPlaybackMode mode = AdvancedPlaybackMode.Index;

        [SerializeField, Tooltip("Запускать при включении объекта.")]
        private bool autoStart = true;

        /// <summary>
        /// Номер текущего трека. -1 — ещё ни один не выбирался
        /// </summary>
        private int _current = -1;

        /// <summary>
        /// Недавно выбранные номера — для антиповтора случайного режима
        /// </summary>
        private readonly List<int> _history = new();

        private AudioSampleWrapper _playback;

        /// <summary>
        /// Хэндл клипа, который сейчас играет
        /// </summary>
        private AssetHandle<AudioClip> _playingHandle;

        private bool _manualPause;

        /// <summary>
        /// Хэндлер включён: между <c>OnEnable</c> и <c>OnDisable</c>
        /// </summary>
        private bool _active;

        /// <summary>
        /// Отмена запроса, который ещё грузится; <c>null</c> — сейчас ничего не грузится. У каждого запроса
        /// свой токен: новый запрос и останов отменяют прежний
        /// </summary>
        private CancellationTokenSource _loadCts;

        /// <summary>
        /// Прежний трек при запуске нового сменяет сама аудиосистема (общий слот с затуханием) —
        /// останавливать его отдельной командой не нужно
        /// </summary>
        protected virtual bool ReplacedOnStart => false;

        private void OnEnable()
        {
            _active = true;
            _current = -1;
            _history.Clear();
            _manualPause = false;
            GameController.OnGameStateChanged += ApplyPause;

            if (autoStart)
                PlayNext();
        }

        private void OnDisable()
        {
            _active = false;
            GameController.OnGameStateChanged -= ApplyPause;
            StopTrack();
        }

        /// <summary>
        /// Трек запрашивается только у включённого хэндлера: звук, запущенный на выключенном объекте,
        /// выпал бы из жизненного цикла — его некому было бы остановить
        /// </summary>
        private bool CanRequest(string method)
        {
            if (_active)
                return true;

            Debug.LogError($"[{GetType().Name}] {method}: хэндлер выключен — запрос трека пропущен.", this);
            return false;
        }

        // ── Управление ────────────────────────────────────────────────────────

        /// <summary>
        /// Запустить трек по номеру в списке
        /// </summary>
        public void Play(int number)
        {
            if (!CanRequest(nameof(Play)))
                return;

            if (number < 0 || number >= clips.Length)
            {
                Debug.LogError($"[{GetType().Name}] Нет трека №{number}: в списке {clips.Length}.", this);
                return;
            }

            _current = number;
            Remember(number);
            StartCurrent();
        }

        /// <summary>
        /// Запустить следующий трек: по списку (после последнего — первый), в случайном режиме — случайный
        /// </summary>
        public void PlayNext()
        {
            if (!CanRequest(nameof(PlayNext)))
                return;

            if (clips.Length == 0)
            {
                Debug.LogError($"[{GetType().Name}] Список клипов пуст.", this);
                return;
            }

            Next();
            StartCurrent();
        }

        /// <summary>
        /// Остановить и отпустить клип
        /// </summary>
        public void Stop() => StopTrack();

        /// <summary>
        /// Ручная пауза. Не зависит от паузы игры
        /// </summary>
        public void Pause()
        {
            _manualPause = true;
            ApplyPause();
        }

        /// <summary>
        /// Снять ручную паузу. При паузе игры звук останется на паузе
        /// </summary>
        public void Unpause()
        {
            _manualPause = false;
            ApplyPause();
        }

        // ── Выбор трека ───────────────────────────────────────────────────────

        /// <summary>
        /// Трек выбирается случайно, а не по списку
        /// </summary>
        private bool IsRandom =>
            mode == AdvancedPlaybackMode.Random || mode == AdvancedPlaybackMode.RandomOneShot;

        /// <summary>
        /// Трек играет один раз: после него звук останавливается, следующий не запускается
        /// </summary>
        private bool IsOneShot =>
            mode == AdvancedPlaybackMode.IndexOneShot || mode == AdvancedPlaybackMode.RandomOneShot;

        /// <summary>
        /// Новое значение <see cref="_current"/> по режиму: случайный выбор либо следующий по списку
        /// </summary>
        private void Next()
        {
            var newNumber = -1;
            if (IsRandom)
            {
                newNumber = PickRandom();
            }
            else
            {
                newNumber = _current + 1;
                if (newNumber >= clips.Length)
                    newNumber = 0;
            }

            _current = newNumber;
        }

        /// <summary>
        /// Случайный номер, не входящий в историю. История хранит половину списка (округляя вниз) —
        /// та же механика, что у звуков базы
        /// </summary>
        private int PickRandom()
        {
            var count = clips.Length;
            var pick = Random.Range(0, count - _history.Count);
            var index = 0;
            for (var i = 0; i < count; i++)
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

            Remember(index);
            return index;
        }

        private void Remember(int index)
        {
            _history.Remove(index);
            _history.Add(index);
            var size = clips.Length / 2;
            while (_history.Count > size)
                _history.RemoveAt(0);
        }

        // ── Запуск и останов ──────────────────────────────────────────────────

        /// <summary>
        /// Запрос трека. Порядок один для всех хэндлеров: загрузка клипа → отвязка прежнего трека (с остановом,
        /// если его не сменяет сама аудиосистема) → воспроизведение. Пока клип грузится, прежний трек играет. Загрузка сорвалась — ошибка в лог
        /// и останов прежнего трека. Новый запрос отменяет прежний, ещё не исполненный: его метод
        /// завершится по отмене, не дойдя до запуска
        /// </summary>
        private void StartCurrent()
        {
            var number = _current;
            var handle = clips[number];
            if (handle == null)
            {
                Debug.LogError($"[{GetType().Name}] Трек №{number} не задан.", this);
                StopTrack();
                return;
            }

            CancelLoad();
            _loadCts = new CancellationTokenSource();
            LoadAndPlay(handle, number, _loadCts.Token).Forget();
        }

        /// <summary>
        /// Отменить запрос, который ещё грузится
        /// </summary>
        private void CancelLoad()
        {
            var cts = _loadCts;
            if (cts == null)
                return;

            _loadCts = null;
            cts.Cancel();
            cts.Dispose();
        }

        private async UniTaskVoid LoadAndPlay(AssetHandle<AudioClip> handle, int number, CancellationToken ct)
        {
            AudioClip clip;
            try
            {
                //Клип уже в памяти (играющий трек либо прямая ссылка) — берётся без загрузки, новое удержание не ставится
                clip = handle.IsLoaded ? handle.Asset : await handle.LoadAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                //Запрос отменён. Своё удержание клипа снял сам кэш; Release тут нельзя — он снимет
                //и удержание нового запроса, если тот грузит этот же трек (тот же хэндл)
                return;
            }
            catch (Exception ex)
            {
                //Исходное исключение — вложенным: в консоли видны и хэндлер с номером трека, и причина со стеком
                Debug.LogException(
                    new Exception($"[{GetType().Name}] Трек №{number}: клип не загружен.", ex), this);
                handle.Release();
                if (!ct.IsCancellationRequested) StopTrack();
                return;
            }

            //Клип получен — запрос больше не грузится
            _loadCts.Dispose();
            _loadCts = null;

            if (clip == null)
            {
                Debug.LogError($"[{GetType().Name}] Трек №{number}: пустой клип.", this);
                StopTrack();
                return;
            }

            // ── Останов прежнего трека ──
            StopPrevious(handle);


            // ── Воспроизведение ──
            AudioSampleWrapper playback = null;
            try
            {
                playback = StartPlayback(clip, !IsOneShot);
            }
            finally
            {
                //Запуск не состоялся — клип держать незачем
                if (playback == null)
                    handle.Release();
            }

            if (playback == null)
                return;

            _playback = playback;
            _playingHandle = handle;
            _playback.OnPlay += ApplyPause;
            _playback.OnLoopRestart += HandleLoopRestart;
            _playback.OnFinished += HandleFinished;
            ApplyPause();
        }

        /// <summary>
        /// Отвязать прежний трек перед запуском нового и остановить его, если аудиосистема не сменяет его сама
        /// (<see cref="ReplacedOnStart"/>). Его клип отпускается, если новый трек — не тот же хэндл
        /// </summary>
        private void StopPrevious(AssetHandle<AudioClip> next)
        {
            var playback = Detach();
            if (playback != null && playback.State != PlaybackState.Finished && !ReplacedOnStart)
                StopPlayback(playback);

            var handle = _playingHandle;
            _playingHandle = null;
            if (handle != null && handle != next)
                handle.Release();
        }

        /// <summary>
        /// Остановить звук и отпустить клип. Запрос, который ещё грузится, отменяется
        /// </summary>
        private void StopTrack()
        {
            CancelLoad();
            var playback = Detach();
            if (playback != null && playback.State != PlaybackState.Finished)
                StopPlayback(playback);

            var handle = _playingHandle;
            _playingHandle = null;
            if (handle != null)
                handle.Release();
        }

        /// <summary>
        /// Зацикленный клип доиграл и пошёл на новый круг. В режимах «один раз» (<see cref="IsOneShot"/>)
        /// звук останавливается, в остальных запускается следующий трек. Пока грузится запрошенный клип,
        /// конец прежнего трека запрос не трогает
        /// </summary>
        private void HandleLoopRestart()
        {
            if (_loadCts != null)
                return;

            if (IsOneShot)
            {
                StopTrack();
                return;
            }

            var previous = _current;
            Next();
            //Следующим выпал тот же трек (в списке один клип) — он уже сам пошёл на новый круг
            if (_current != previous)
                StartCurrent();
        }

        /// <summary>
        /// Аудиосистема завершила трек: незацикленный доиграл либо звук сняли снаружи (вытеснение из слота
        /// музыки, общая очистка звуков). Пока грузится запрошенный клип, запрос остаётся в силе
        /// </summary>
        private void HandleFinished()
        {
            if (_loadCts == null)
                StopTrack();
        }

        /// <summary>
        /// Снять подписки с играющего трека и забыть его. Сам звук не трогает
        /// </summary>
        private AudioSampleWrapper Detach()
        {
            var playback = _playback;
            _playback = null;
            if (playback == null)
                return null;

            playback.OnPlay -= ApplyPause;
            playback.OnLoopRestart -= HandleLoopRestart;
            playback.OnFinished -= HandleFinished;
            return playback;
        }

        // ── Пауза ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Привести играющий трек к паузам: игры и ручной. Идемпотентно
        /// </summary>
        private void ApplyPause()
        {
            //Доигравший трек остаётся текущим, пока грузится следующий, — приводить к паузам в нём нечего
            var playback = _playback;
            if (playback == null || playback.State == PlaybackState.Finished)
                return;

            if (_manualPause || GameController.GetState() == GameStates.Paused)
                playback.Pause();
            else if (playback.State == PlaybackState.Paused)
                playback.Play();
        }

        // ── Выход звука — у наследника ────────────────────────────────────────

        /// <summary>
        /// Запустить клип. Возвращает управление треком либо <c>null</c>, если запуск не состоялся
        /// </summary>
        /// <param name="clip">Клип трека</param>
        /// <param name="loop">
        /// Зациклить клип: за ним должен запуститься следующий трек, конец круга придёт рестартом. Без
        /// зацикливания клип играет один раз и завершается сам. Выход, который играет только зацикленно,
        /// параметр не использует — хэндлер остановит трек по рестарту
        /// </param>
        protected abstract AudioSampleWrapper StartPlayback(AudioClip clip, bool loop);

        /// <summary>
        /// Остановить ещё не завершённый трек
        /// </summary>
        protected abstract void StopPlayback(AudioSampleWrapper playback);
    }
}