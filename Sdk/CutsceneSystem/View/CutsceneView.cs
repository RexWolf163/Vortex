using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Spine;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.CutsceneSystem.Models;
using Vortex.Unity.AudioSystem.Model;
using AnimationState = Spine.AnimationState;
using Event = Spine.Event;

namespace Vortex.Sdk.CutsceneSystem.View
{
    /// <summary>
    /// Представление катсцены: показывает открытую в <see cref="CutsceneController"/> катсцену на своём
    /// Spine-компоненте и озвучивает её. Владеет загрузкой: сам грузит ассеты катсцены через их
    /// <see cref="AssetHandle{T}"/> и сам отпускает.
    ///
    /// Порядок показа. Сначала грузится стартовый набор — скелет, музыка и эмбиент текущей фазы; после него
    /// запускаются анимация и звук. Эмбиенты прочих фаз догружаются в фоне. Фаза переключается сразу;
    /// её эмбиент, если ещё грузится, включается по окончании загрузки. Звуки событий анимации — записи общей
    /// базы звуков: они не грузятся и не отпускаются, только играются и останавливаются вместе с показом.
    ///
    /// Анимация фазы всегда зациклена. Сквозная фаза (без <see cref="CutscenePhase.AnimationLooped"/>),
    /// отыграв проход, сообщает об этом контроллеру — фазу переключает он.
    ///
    /// Жизненный цикл: на <c>OnEnable</c> подписывается на контроллер и подхватывает уже открытую катсцену
    /// с её текущей фазы; на закрытие катсцены и на <c>OnDisable</c> останавливает анимацию и свои звуки,
    /// отменяет незавершённые загрузки и отпускает всё загруженное.
    ///
    /// Пауза. На <see cref="GameStates.Paused"/> и <see cref="GameStates.Loading"/> представление ставит на паузу
    /// свой звук. Скелет оно не замораживает — это делает <c>SpinePauseHandler</c> (SpineExtensions) на том же
    /// объекте, по тем же состояниям игры.
    ///
    /// Активное представление одно: второе включённое одновременно в показе не участвует (ошибка в лог) —
    /// иначе оба грузили бы и отпускали одни и те же хэндлы
    /// </summary>
    public class CutsceneView : MonoBehaviour
    {
        [SerializeField, Tooltip("Скелет катсцены в мире. Задаётся либо он, либо Skeleton Graphic.")]
        private SkeletonAnimation skeletonAnimation;

        [SerializeField, Tooltip("Скелет катсцены в UI. Задаётся либо он, либо Skeleton Animation.")]
        private SkeletonGraphic skeletonGraphic;

        private static CutsceneView _active;

        /// <summary>
        /// Показываемая катсцена. <c>null</c> — показа нет
        /// </summary>
        private CutsceneData _cutscene;

        private CancellationTokenSource _cts;

        /// <summary>
        /// Стартовый набор загружен, скелет установлен
        /// </summary>
        private bool _ready;

        private AnimationState _state;

        private readonly List<AssetHandle<AudioClip>> _clipHandles = new();
        private readonly HashSet<CutsceneSound> _requested = new();
        private readonly Dictionary<CutsceneSound, SoundClip> _sounds = new();

        /// <summary>
        /// Эмбиент, который должен звучать на текущей фазе
        /// </summary>
        private CutsceneSound _ambient;

        private AudioSampleWrapper _ambientPlayback;
        private readonly List<AudioSampleWrapper> _eventPlaybacks = new();

        /// <summary>
        /// Музыка показа: клип выбран один раз, после паузы звучит он же
        /// </summary>
        private SoundClipFixed _music;

        private bool _musicPlaying;

        /// <summary>
        /// Скелет установлен на компонент — на остановке показа его нужно снять
        /// </summary>
        private bool _skeletonSet;

        private static bool IsPaused => GameController.GetState() is GameStates.Paused or GameStates.Loading;

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                Debug.LogError(
                    $"[CutsceneView] Уже активно представление '{_active.name}': '{name}' в показе не участвует.",
                    this);
                return;
            }

            _active = this;
            CutsceneController.OnOpened += StartShow;
            CutsceneController.OnPhaseChanged += ApplyPhase;
            CutsceneController.OnClosed += StopShow;
            GameController.OnGameStateChanged += ApplyPause;

            if (CutsceneController.IsOpen)
                StartShow();
        }

        private void OnDisable()
        {
            if (_active != this)
                return;

            CutsceneController.OnOpened -= StartShow;
            CutsceneController.OnPhaseChanged -= ApplyPhase;
            CutsceneController.OnClosed -= StopShow;
            GameController.OnGameStateChanged -= ApplyPause;

            StopShow();
            _active = null;
        }

        // ── Показ ─────────────────────────────────────────────────────────────

        private void StartShow()
        {
            StopShow();

            if ((skeletonAnimation == null) == (skeletonGraphic == null))
            {
                Debug.LogError(
                    "[CutsceneView] Должен быть задан ровно один скелет: Skeleton Animation либо Skeleton Graphic.",
                    this);
                return;
            }

            var cutscene = CutsceneController.Data.Cutscene;
            if (cutscene.Skeleton == null)
            {
                Debug.LogError($"[CutsceneView] У катсцены '{cutscene.name}' не задан скелет.", cutscene);
                return;
            }

            _cutscene = cutscene;
            _cts = new CancellationTokenSource();
            RunAsync(cutscene, _cts.Token).Forget();
        }

        private async UniTaskVoid RunAsync(CutsceneData cutscene, CancellationToken token)
        {
            try
            {
                //Стартовый набор: скелет, музыка, эмбиент текущей фазы
                SkeletonDataAsset skeleton = null;

                async UniTask LoadSkeleton() => skeleton = await cutscene.Skeleton.LoadAsync(token);

                await UniTask.WhenAll(
                    LoadSkeleton(),
                    LoadSound(cutscene.Music, token),
                    LoadSound(ResolveAmbient(cutscene, CutsceneController.Data.PhaseIndex), token));
                token.ThrowIfCancellationRequested();

                if (skeleton == null)
                {
                    Debug.LogError($"[CutsceneView] У катсцены '{cutscene.name}' пустой скелет.", cutscene);
                    return;
                }

                SetSkeleton(skeleton);
                _state.Event += HandleSpineEvent;
                _ready = true;
                ApplyPause();
                ApplyPhase();

                //Эмбиенты остальных фаз — в фоне
                var rest = new List<UniTask>();
                foreach (var phase in cutscene.Phases)
                    rest.Add(LoadSound(phase.Ambient, token));
                await UniTask.WhenAll(rest);
            }
            catch (OperationCanceledException)
            {
                //Показ остановлен до конца загрузки — штатно
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CutsceneView] Катсцена '{cutscene.name}': сбой загрузки. {exception}", this);
            }
        }

        private void StopShow()
        {
            if (_cutscene == null)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;

            StopAmbient();
            foreach (var playback in _eventPlaybacks.ToArray())
                playback.Stop();
            _eventPlaybacks.Clear();
            StopMusic();

            if (_state != null)
                _state.Event -= HandleSpineEvent;
            _state = null;
            if (_skeletonSet)
                ClearSkeleton();

            //Незавершённые загрузки сняты отменой токена; Release у незагруженного хэндла — no-op
            foreach (var handle in _clipHandles)
                handle.Release();
            _clipHandles.Clear();
            _cutscene.Skeleton.Release();

            _requested.Clear();
            _sounds.Clear();
            _ambient = null;
            _music = null;
            _ready = false;
            _cutscene = null;
        }

        // ── Загрузка звуков ───────────────────────────────────────────────────

        /// <summary>
        /// Загрузить клипы звука и собрать из них <see cref="SoundClip"/> (выбор клипа с антиповтором — его).
        /// Повторный запрос того же звука в рамках показа — no-op. Сбой загрузки пишется в лог, показ идёт
        /// без этого звука
        /// </summary>
        private async UniTask LoadSound(CutsceneSound sound, CancellationToken token)
        {
            if (sound == null || sound.IsEmpty || !_requested.Add(sound))
                return;

            var handles = sound.Clips.Where(handle => handle != null).ToArray();
            _clipHandles.AddRange(handles);
            try
            {
                var loaded = await UniTask.WhenAll(handles.Select(handle => handle.LoadAsync(token)));
                token.ThrowIfCancellationRequested();

                var clips = loaded.Where(clip => clip != null).ToArray();
                if (clips.Length == 0)
                    return;

                _sounds[sound] = new SoundClip(clips, Vector2.one, Vector2.one, false, sound.Channel,
                    sound.AntiRepeat);
                TryPlayAmbient();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CutsceneView] Катсцена '{_cutscene?.name}': звук не загружен. {exception}", this);
            }
        }

        // ── Фазы ──────────────────────────────────────────────────────────────

        private void ApplyPhase()
        {
            if (!_ready)
                return;

            var index = CutsceneController.Data.PhaseIndex;
            var phase = _cutscene.Phases[index];

            if (_state.Data.SkeletonData.FindAnimation(phase.AnimationKey) == null)
            {
                Debug.LogError(
                    $"[CutsceneView] Катсцена '{_cutscene.name}', фаза {index}: в скелете нет анимации '{phase.AnimationKey}'.",
                    _cutscene);
            }
            else
            {
                var entry = _state.SetAnimation(0, phase.AnimationKey, true);
                //Сквозная фаза: отыграла проход — сообщаем контроллеру, фазу переключает он
                if (!phase.AnimationLooped)
                    entry.Complete += _ => CutsceneController.CompletePhase(index);
            }

            var ambient = ResolveAmbient(_cutscene, index);
            if (ambient != _ambient)
            {
                StopAmbient();
                _ambient = ambient;
            }

            TryPlayAmbient();
        }

        /// <summary>
        /// Эмбиент, звучащий на фазе <paramref name="index"/>: свой, либо унаследованный от ближайшей прошлой
        /// фазы с эмбиентом — пока по пути нет фазы с <see cref="CutscenePhase.EmptyAmbientMode.Stop"/>
        /// </summary>
        private static CutsceneSound ResolveAmbient(CutsceneData cutscene, int index)
        {
            for (var i = index; i >= 0; i--)
            {
                var phase = cutscene.Phases[i];
                if (!phase.Ambient.IsEmpty)
                    return phase.Ambient;
                if (phase.EmptyAmbient == CutscenePhase.EmptyAmbientMode.Stop)
                    return null;
            }

            return null;
        }

        // ── Звук ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Запустить эмбиент текущей фазы, если он загружен и ещё не звучит. Зовётся на смене фазы, по загрузке
        /// каждого звука и на снятии паузы
        /// </summary>
        private void TryPlayAmbient()
        {
            if (!_ready || _ambient == null || IsAlive(_ambientPlayback) || IsPaused)
                return;
            if (!_sounds.TryGetValue(_ambient, out var clip))
                return;

            _ambientPlayback = AudioController.PlaySoundWithControl(new SoundClipFixed(clip, true, (string)null), true);
        }

        private void StopAmbient()
        {
            if (IsAlive(_ambientPlayback))
                _ambientPlayback.Stop();
            _ambientPlayback = null;
        }

        private void PlayMusic()
        {
            if (_musicPlaying || !_sounds.TryGetValue(_cutscene.Music, out var clip))
                return;

            _music ??= new SoundClipFixed(clip, true, (string)null);
            AudioController.PlayCoverMusic(_music);
            _musicPlaying = true;
        }

        private void StopMusic()
        {
            if (!_musicPlaying)
                return;

            AudioController.StopCoverMusic();
            _musicPlaying = false;
        }

        private void HandleSpineEvent(TrackEntry trackEntry, Event e)
        {
            string sound = null;
            foreach (var eventData in _cutscene.EventToAudioDatas)
            {
                if (eventData.EventName != e.Data.Name)
                    continue;
                sound = eventData.AudioPack;
                break;
            }

            if (string.IsNullOrEmpty(sound))
                return;

            //Звук события — запись общей базы звуков: загрузки и отпуска не требует
            var playback = AudioController.PlaySoundWithControl(sound);
            if (playback == null)
                return;

            _eventPlaybacks.Add(playback);
            playback.OnFinished += () => _eventPlaybacks.Remove(playback);
        }

        private static bool IsAlive(AudioSampleWrapper playback) =>
            playback != null && playback.State != PlaybackState.Finished;

        // ── Пауза ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Привести звук к состоянию паузы игры. Идемпотентно: зовётся на любую смену состояния игры
        /// и на старте показа
        /// </summary>
        private void ApplyPause()
        {
            if (!_ready)
                return;

            if (IsPaused)
            {
                if (IsAlive(_ambientPlayback))
                    _ambientPlayback.Pause();
                foreach (var playback in _eventPlaybacks)
                    playback.Pause();
                StopMusic();
                return;
            }

            Resume(_ambientPlayback);
            foreach (var playback in _eventPlaybacks)
                Resume(playback);
            TryPlayAmbient();
            PlayMusic();
        }

        private static void Resume(AudioSampleWrapper playback)
        {
            if (playback != null && playback.State == PlaybackState.Paused)
                playback.Play();
        }

        // ── Скелет ────────────────────────────────────────────────────────────

        private void SetSkeleton(SkeletonDataAsset skeleton)
        {
            _skeletonSet = true;
            if (skeletonAnimation != null)
            {
                skeletonAnimation.skeletonDataAsset = skeleton;
                skeletonAnimation.Initialize(true);
                _state = skeletonAnimation.AnimationState;
            }
            else
            {
                skeletonGraphic.skeletonDataAsset = skeleton;
                skeletonGraphic.Initialize(true);
                _state = skeletonGraphic.AnimationState;
            }
        }

        /// <summary>
        /// Снять скелет с компонента — до отпуска его ассета. Компонент мог быть уже уничтожен вместе со сценой
        /// </summary>
        private void ClearSkeleton()
        {
            _skeletonSet = false;
            if (skeletonAnimation != null)
            {
                skeletonAnimation.ClearState();
                skeletonAnimation.skeletonDataAsset = null;
                skeletonAnimation.Initialize(true);
            }
            else if (skeletonGraphic != null)
            {
                skeletonGraphic.Clear();
                skeletonGraphic.skeletonDataAsset = null;
            }
        }
    }
}
