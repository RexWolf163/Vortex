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
    /// останов и отпуск клипа. В памяти держится только клип играющего трека.
    ///
    /// Пауз две, независимых: пауза игры (<see cref="GameStates.Paused"/>) и ручная (<see cref="Pause"/> /
    /// <see cref="Unpause"/>). Звук играет, только когда нет ни одной.
    ///
    /// Публичные методы рассчитаны на вызов из состояний <c>UIStateSwitcher</c> (<c>EventFire</c>).
    /// Куда именно идёт звук, решает наследник
    /// </summary>
    public abstract class AdvancedAudioHandler : MonoBehaviour
    {
        [SerializeReference, Tooltip("Клипы. В памяти держится только клип играющего трека.")]
        private AssetHandle<AudioClip>[] clips = new AssetHandle<AudioClip>[0];

        [SerializeField, Tooltip(
             "Index — выбранный трек в цикле; Random — после окончания трека случайный; OneShot — трек один раз.")]
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

        private CancellationTokenSource _loading;

        /// <summary>
        /// Хэндлы, ожидающие отложенного отпуска
        /// </summary>
        private readonly Dictionary<AssetHandle<AudioClip>, CancellationTokenSource> _pendingRelease = new();

        private bool _manualPause;

        protected AdvancedPlaybackMode Mode => mode;

        /// <summary>
        /// Управление играющим треком. <c>null</c> — ничего не играет
        /// </summary>
        protected AudioSampleWrapper Playback => _playback;

        /// <summary>
        /// Не глушить прошлый трек, пока грузится новый: его сменит сама аудиосистема
        /// </summary>
        protected virtual bool KeepPlayingWhileLoading => false;

        /// <summary>
        /// Сколько секунд клип ещё нужен после команды останова (затухание). 0 — отпускается сразу
        /// </summary>
        protected virtual float ReleaseDelay => 0f;

        private void OnEnable()
        {
            _current = -1;
            _history.Clear();
            _manualPause = false;
            GameController.OnGameStateChanged += ApplyPause;

            if (autoStart)
                PlayNext();
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= ApplyPause;
            StopTrack();
        }

        // ── Управление ────────────────────────────────────────────────────────

        /// <summary>
        /// Запустить трек по номеру в списке
        /// </summary>
        public void Play(int number)
        {
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
        /// Новое значение <see cref="_current"/> по режиму: случайный выбор либо следующий по списку
        /// </summary>
        private void Next()
        {
            if (mode == AdvancedPlaybackMode.Random)
            {
                _current = PickRandom();
                return;
            }

            _current++;
            if (_current >= clips.Length)
                _current = 0;
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

        private void StartCurrent()
        {
            CancelLoading();

            var handle = clips[_current];
            if (handle == null)
            {
                Debug.LogError($"[{GetType().Name}] Трек №{_current} не задан.", this);
                StopTrack();
                return;
            }

            if (!KeepPlayingWhileLoading)
                StopTrack();

            _loading = new CancellationTokenSource();
            LoadAndPlay(handle, _loading.Token).Forget();
        }

        private async UniTaskVoid LoadAndPlay(AssetHandle<AudioClip> handle, CancellationToken token)
        {
            AudioClip clip;
            try
            {
                //Клип мог ждать отложенного отпуска — тогда он ещё в памяти и грузить его заново не нужно
                CancelRelease(handle);
                clip = handle.IsLoaded ? handle.Asset : await handle.LoadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{GetType().Name}] Трек №{_current}: клип не загружен. {exception}", this);
                StopTrack();
                return;
            }

            if (token.IsCancellationRequested)
            {
                ReleaseHandle(handle);
                return;
            }

            if (clip == null)
            {
                Debug.LogError($"[{GetType().Name}] Трек №{_current}: пустой клип.", this);
                StopTrack();
                return;
            }

            var previousHandle = _playingHandle;
            Detach();

            _playback = StartPlayback(clip, mode == AdvancedPlaybackMode.Index);
            if (previousHandle != null && previousHandle != handle)
                ReleaseHandle(previousHandle);

            //Запуск не состоялся — клип держать незачем
            if (_playback == null)
            {
                ReleaseHandle(handle);
                return;
            }

            _playingHandle = handle;
            _playback.OnPlay += ApplyPause;
            _playback.OnFinished += HandleFinished;
            ApplyPause();
        }

        /// <summary>
        /// Остановить звук, снять незавершённую загрузку и отпустить клип
        /// </summary>
        protected void StopTrack()
        {
            CancelLoading();

            var playback = Detach();
            if (playback != null && playback.State != PlaybackState.Finished)
                StopPlayback(playback);

            var handle = _playingHandle;
            _playingHandle = null;
            if (handle != null)
                ReleaseHandle(handle);
        }

        /// <summary>
        /// Трек доиграл: в случайном режиме запускается следующий, в остальных звук останавливается
        /// </summary>
        protected void CompleteTrack()
        {
            if (mode == AdvancedPlaybackMode.Random)
                PlayNext();
            else
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
            playback.OnFinished -= HandleFinished;
            return playback;
        }

        private void HandleFinished() => OnPlaybackFinished();

        private void CancelLoading()
        {
            if (_loading == null)
                return;

            _loading.Cancel();
            _loading.Dispose();
            _loading = null;
        }

        // ── Отпуск клипов ─────────────────────────────────────────────────────

        private void ReleaseHandle(AssetHandle<AudioClip> handle)
        {
            var delay = ReleaseDelay;
            if (delay <= 0f)
            {
                handle.Release();
                return;
            }

            CancelRelease(handle);
            var source = new CancellationTokenSource();
            _pendingRelease[handle] = source;
            ReleaseLater(handle, delay, source).Forget();
        }

        /// <summary>
        /// Отложенный отпуск. Не привязан к жизни объекта: клип должен дожить до конца затухания, даже если
        /// хэндлер уже выключен или уничтожен
        /// </summary>
        private async UniTaskVoid ReleaseLater(AssetHandle<AudioClip> handle, float delay,
            CancellationTokenSource source)
        {
            var canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(delay), DelayType.UnscaledDeltaTime, cancellationToken: source.Token)
                .SuppressCancellationThrow();
            if (canceled)
                return;

            _pendingRelease.Remove(handle);
            source.Dispose();
            handle.Release();
        }

        private void CancelRelease(AssetHandle<AudioClip> handle)
        {
            if (!_pendingRelease.Remove(handle, out var source))
                return;

            source.Cancel();
            source.Dispose();
        }

        // ── Пауза ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Привести играющий трек к паузам: игры и ручной. Идемпотентно
        /// </summary>
        private void ApplyPause()
        {
            var playback = _playback;
            if (playback == null)
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
        protected abstract AudioSampleWrapper StartPlayback(AudioClip clip, bool loop);

        /// <summary>
        /// Остановить ещё не завершённый трек
        /// </summary>
        protected abstract void StopPlayback(AudioSampleWrapper playback);

        /// <summary>
        /// Аудиосистема сообщила о завершении трека
        /// </summary>
        protected abstract void OnPlaybackFinished();
    }
}
