using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Enums;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.Extensions.Abstractions;
using Vortex.Unity.UI.PoolSystem;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace Vortex.Unity.AudioSystem
{
    /// <summary>
    /// Центральный контроллер управления звуками и музыкой.
    ///
    /// Считается что музыка может быть одновременно только одна.
    ///
    /// Основной музыкальный трек может быть временно отключен для ситуативного
    /// При отключении ситуативного трека вернется основная тема
    /// 
    /// </summary>
    public class AudioPlayer : MonoBehaviourSingleton<AudioPlayer>
    {
        [InfoBox("Пул звуков")] [SerializeField]
        private Pool pool;

        [InfoBox("Основной проигрыватель музыки")] [SerializeField]
        private MusicPlayer musicPlayer;

        [InfoBox("Ситуативный проигрыватель музыки. Для перекрытия основного фона ситуативным треком")] [SerializeField]
        private MusicPlayer musicCoverPlayer;

        [SerializeField, Range(0f, 3f)] private float musicFadeTime = 1f;

        public static float MusicFadeTime => Instance.musicFadeTime;

        private static readonly AsyncTween FadeTween = new();
        private static readonly AsyncTween FadeCoverTween = new();

        private static bool _needFadingStart;
        private static bool _needFadingEnd;

        private static bool _needCoverFadingEnd;

        /// <summary>
        /// Флаг состояния для основного канала музыки, чтобы
        /// рефлексировать при обработке команды на сутуативную музыку
        /// </summary>
        private static bool _isMusicPlaying;

        /// <summary>
        /// Кешированная ссылка на текущую ситуативную музыку
        /// </summary>
        private AudioClip _coverMusic;

        /// <summary>
        /// Кешированная ссылка на текущую музыку
        /// </summary>
        private AudioClip _music;

        protected override void OnDestroy()
        {
            StopAllSounds();
            if (Instance != null && Instance.musicPlayer != null && Instance.musicPlayer.IsPlay())
                StopMusic();
            if (Instance != null && Instance.musicCoverPlayer != null && Instance.musicCoverPlayer.IsPlay())
                StopCoverMusic();
            FadeTween.Kill();
            FadeCoverTween.Kill();
            base.OnDestroy();
        }

        // Завершение приложения → полный останов. Ставится ДО teardown-цепочки (AppStateHandler.OnDestroy →
        // App.Exit → OnExit → CutsceneController.Dispose → StopCoverMusic → RestoreMainMusic), поэтому основная
        // тема на прерывании (в т.ч. на секс-сцене поверх cover-музыки) больше не «воскресает» на выходе.
        [RuntimeInitializeOnLoadMethod]
        private static void HookApplicationQuit()
        {
            App.OnExit -= StopAll;
            App.OnExit += StopAll;
        }

        /// <summary>
        /// Полный немедленный останов всех звуков и музыки БЕЗ восстановления основной темы.
        /// </summary>
        internal static void StopAll()
        {
            if (Instance == null)
                return;

            FadeTween.Kill();
            FadeCoverTween.Kill();
            _isMusicPlaying = false; // RestoreMainMusic гейтится этим флагом → тему не поднимет
            if (Instance.pool != null)
                Instance.pool.Clear();
            if (Instance.musicPlayer != null)
                Instance.musicPlayer.Stop();
            if (Instance.musicCoverPlayer != null)
                Instance.musicCoverPlayer.Stop();
        }

        #region Sound

        internal static AudioSampleWrapper PlaySound(object sound, bool loop = false, string channelOverrideName = null,
            bool withControl = false)
        {
            if (Instance == null)
                return null;
            SoundClipFixed clip;
            switch (sound)
            {
                case SoundClipFixed sf:
                    clip = sf;
                    break;
                case string id:
                    var data = Database.GetRecord<Sound>(id);
                    if (data != null)
                    {
                        var s = data.Sample;
                        try
                        {
                            var channel = channelOverrideName.IsNullOrWhitespace()
                                ? s.Channel.Name
                                : channelOverrideName;
                            clip = new SoundClipFixed(s, loop, channel);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogError($"[{data.Name}] {exception.Message}");
                            return null;
                        }

                        break;
                    }

                    Debug.LogError($"[AudioPlayer] Unknown sound ID: {id}");
                    return null;
                case Sound s:
                    clip = new SoundClipFixed(s.Sample, loop, channelOverrideName);
                    break;
                case AudioClip a:
                    clip = new SoundClipFixed(a, loop, channelOverrideName);
                    break;
                default:
                    Debug.LogError($"[AudioPlayer] Unknown sound type: {sound.GetType().Name}");
                    return null;
            }

            // Хэндл создаём только по запросу (fire-and-forget его не аллоцирует). При наличии — кладём его
            // в данные пул-элемента ВМЕСТЕ с клипом (составной ключ), чтобы AudioSourceHandler нашёл и подключил.
            SoundWrapper wrapper = null;
            object poolData = clip;
            if (withControl)
            {
                wrapper = new SoundWrapper(clip.GetClip(), clip.GetDuration(), loop);
                poolData = new object[] { clip, wrapper };
            }

            Instance.pool.AddItem(poolData);
            return wrapper;
        }

        internal static void StopAllSounds(string channel = null)
        {
            if (channel == null)
                Instance.pool.Clear();
            else
                Instance.pool.RemoveByCallback(item => ExtractClip(item)?.Channel?.Name == channel);
        }

        // Ключ пул-элемента звука — либо сам SoundClipFixed, либо составной [clip, wrapper] (PlaySoundWithControl).
        private static SoundClipFixed ExtractClip(object poolKey) =>
            poolKey as SoundClipFixed ?? (poolKey as object[])?[0] as SoundClipFixed;

        #endregion

        #region Music

        private static bool FadeForMusic(Action callback)
        {
            if (Instance.musicPlayer.IsPlay() && Instance.musicPlayer.GetVolumeMultiplier() > 0)
            {
                FadeTween.Kill();
                if (_needFadingEnd)
                {
                    FadeTween.Set(Instance.musicPlayer.GetVolumeMultiplier, Instance.musicPlayer.SetVolumeMultiplier,
                            0f, Instance.musicFadeTime)
                        .SetEase(EaseType.Linear)
                        .OnComplete(callback)
                        .Run();
                    return false;
                }

                Instance.musicPlayer.SetVolumeMultiplier(0);
            }

            return true;
        }

        /// <summary>
        /// Воспроизведение музыки
        /// </summary>
        /// <param name="music"></param>
        /// <param name="fadingStart"></param>
        /// <param name="fadingEnd"></param>
        /// <param name="overrideChannel"></param>
        internal static AudioSampleWrapper PlayMusic(object music,
            bool fadingStart = true,
            bool fadingEnd = true,
            string overrideChannel = null)
        {
#if UNITY_EDITOR
            if (App.GetState() == AppStates.Stopping)
                return null;
#endif
            _needFadingStart = fadingStart;
            _isMusicPlaying = true;

            if (Instance == null)
                return null;

            var clip = GetMusicClip(music, overrideChannel);
            Instance._music = clip.GetClip();

            // Единый владелец: один хэндл (Pending) готовим заранее и возвращаем ЕГО во всех ветках —
            // и при мгновенном старте, и при отложенном (после фейд-аута предыдущего трека).
            var wrapper = Instance.musicPlayer.Prepare(clip);

            if (!FadeForMusic(() =>
                {
                    Instance.musicPlayer.StopAudio(); // глушим предыдущий источник, НЕ трогая новый хэндл
                    PlayMusicInternal(clip, fadingStart, fadingEnd);
                }))
                return wrapper;

            PlayMusicInternal(clip, fadingStart, fadingEnd);
            return wrapper;
        }

        private static void PlayMusicInternal(SoundClip clip, bool fadingStart, bool fadingEnd)
        {
            if (Instance == null)
                return;

            _needFadingEnd = fadingEnd;
            FadeTween.Kill();
            if (fadingStart)
                FadeTween.Set(() => 0f, Instance.musicPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .Run();
            else
                Instance.musicPlayer.SetVolumeMultiplier(1);

            Instance.musicPlayer.Play(clip); // стартует ранее подготовленный хэндл (wrapper.Play())
        }

        /// <summary>
        /// Остановка музыки
        /// </summary>
        internal static void StopMusic()
        {
            _isMusicPlaying = false;
            if (Instance == null)
                return;
            Instance._music = null;
            FadeTween.Kill();
            if (_needFadingEnd)
                FadeTween.Set(Instance.musicPlayer.GetVolumeMultiplier, Instance.musicPlayer.SetVolumeMultiplier, 0f,
                        Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .OnComplete(() =>
                    {
                        if (Instance._music == null)
                            Instance.musicPlayer.Stop();
                    })
                    .Run();
            else
                Instance.musicPlayer.Stop();
        }

        #endregion

        #region CoverMusic

        private static bool FadeForCoverMusic(Action callback)
        {
            if (Instance.musicCoverPlayer.IsPlay() && Instance.musicCoverPlayer.GetVolumeMultiplier() > 0)
            {
                FadeCoverTween.Kill();
                if (_needCoverFadingEnd)
                {
                    FadeCoverTween.Set(Instance.musicCoverPlayer.GetVolumeMultiplier,
                            Instance.musicCoverPlayer.SetVolumeMultiplier, 0f, Instance.musicFadeTime)
                        .SetEase(EaseType.Linear)
                        .OnComplete(callback)
                        .Run();
                    return false;
                }

                Instance.musicCoverPlayer.SetVolumeMultiplier(0);
            }
            else if (_isMusicPlaying && Instance.musicPlayer.GetVolumeMultiplier() > 0)
            {
                FadeTween.Kill();
                if (_needFadingEnd)
                {
                    FadeTween.Set(Instance.musicPlayer.GetVolumeMultiplier,
                            Instance.musicPlayer.SetVolumeMultiplier,
                            0f, Instance.musicFadeTime)
                        .SetEase(EaseType.Linear)
                        .Run();
                    var temp = 1f;
                    FadeCoverTween.Set(() => temp, (a) => temp = a, 0f, Instance.musicFadeTime)
                        .SetEase(EaseType.Linear)
                        .OnComplete(callback)
                        .Run();
                    return false;
                }

                Instance.musicPlayer.SetVolumeMultiplier(0);
            }

            return true;
        }

        /// <summary>
        /// Запуск ситуативной музыки
        /// </summary>
        /// <param name="music"></param>
        /// <param name="fadingStart"></param>
        /// <param name="fadingEnd"></param>
        /// <param name="defaultChannel"></param>
        internal static AudioSampleWrapper PlayCoverMusic(object music, bool fadingStart = true, bool fadingEnd = true,
            string defaultChannel = null)
        {
#if UNITY_EDITOR
            if (App.GetState() == AppStates.Stopping)
                return null;
#endif
            if (Instance == null)
                return null;

            var clip = GetMusicClip(music, defaultChannel);

            Instance._coverMusic = clip.GetClip();

            // Единый владелец хэндла ситуативного трека (см. PlayMusic).
            var wrapper = Instance.musicCoverPlayer.Prepare(clip);

            if (!FadeForCoverMusic(() =>
                {
                    // Держатель снял хэндл, пока слот ждал затухания: трек не запускаем — слот гасим
                    // и возвращаем основную тему, как при StopCoverMusic
                    if (wrapper.State == PlaybackState.Finished && Instance != null &&
                        Instance.musicCoverPlayer.IsCurrent(wrapper))
                    {
                        CancelPendingCoverMusic();
                        return;
                    }

                    PlayCoverMusicInternal(clip, fadingStart, fadingEnd);
                }))
                return wrapper;

            PlayCoverMusicInternal(clip, fadingStart, fadingEnd);
            return wrapper;
        }

        private static void PlayCoverMusicInternal(SoundClip clip, bool fadingStart, bool fadingEnd)
        {
            if (Instance == null)
                return;

            _needCoverFadingEnd = fadingEnd;
            FadeCoverTween.Kill();
            if (fadingStart)
                FadeCoverTween.Set(() => 0f, Instance.musicCoverPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .Run();
            else
                Instance.musicCoverPlayer.SetVolumeMultiplier(1);

            Instance.musicCoverPlayer.Play(clip); // стартует ранее подготовленный хэндл (wrapper.Play())
        }

        /// <summary>
        /// Отложенный старт ситуативного трека отменён держателем хэндла: слот гасится, основная тема
        /// возвращается (если она есть). Затухание к этому моменту уже завершено
        /// </summary>
        private static void CancelPendingCoverMusic()
        {
            Instance._coverMusic = null;
            Instance.musicCoverPlayer.Stop();
            RestoreMainMusic();
        }

        /// <summary>
        /// Останов ситуативной музыки
        /// Будет запущена основная тема (если она есть)
        /// </summary>
        internal static void StopCoverMusic()
        {
            if (Instance == null || !Instance.musicCoverPlayer.IsPlay())
                return;

            Instance._coverMusic = null;
            FadeCoverTween.Kill();
            if (_needCoverFadingEnd)
                FadeCoverTween.Set(Instance.musicCoverPlayer.GetVolumeMultiplier,
                        Instance.musicCoverPlayer.SetVolumeMultiplier, 0f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .OnComplete(() =>
                    {
                        if (Instance._coverMusic == null)
                        {
                            Instance.musicCoverPlayer.Stop();
                            RestoreMainMusic();
                        }
                    }).Run();
            else
            {
                Instance.musicCoverPlayer.Stop();
                RestoreMainMusic();
            }
        }

        private static void RestoreMainMusic()
        {
            if (Instance == null)
                return;

            if (!_isMusicPlaying) return;

            FadeTween.Kill();
            if (_needFadingStart)
                FadeTween.Set(() => 0f, Instance.musicPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .Run();
            else
                Instance.musicPlayer.SetVolumeMultiplier(1);
        }

        #endregion

        private static SoundClipFixed GetMusicClip(object music, string overrideChannel)
        {
            switch (music)
            {
                case string id:
                    var data = Database.GetRecord<Music>(id);
                    if (data != null)
                    {
                        var sample = data.Sample;
                        return new SoundClipFixed(sample, sample.IsLoop(), overrideChannel);
                    }

                    Debug.LogError($"[AudioPlayer] Unknown music ID: {id}");
                    return null;
                case Music m:
                {
                    var sample = m.Sample;
                    return new SoundClipFixed(sample, sample.IsLoop(), overrideChannel);
                }
                case SoundClip s:
                    return new SoundClipFixed(s, s.IsLoop(), overrideChannel);
                case AudioClip a:
                    return new SoundClipFixed(a, true, overrideChannel);
                default:
                    Debug.LogError($"[AudioPlayer] Unknown sound type: {music.GetType().Name}");
                    return null;
            }
        }
    }
}