using System;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.EditorTools.Attributes;
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
        [InfoBubble("Пул звуков")] [SerializeField]
        private Pool pool;

        [InfoBubble("Основной проигрыватель музыки")] [SerializeField]
        private MusicPlayer musicPlayer;

        [InfoBubble("Ситуативный проигрыватель музыки. Для перекрытия основного фона ситуативным треком")]
        [SerializeField]
        private MusicPlayer musicCoverPlayer;

        [SerializeField, Range(0f, 3f)] private float musicFadeTime = 1f;

        public static float MusicFadeTime => Instance.musicFadeTime;

        private static readonly AsyncTween FadeTween = new();
        private static readonly AsyncTween FadeCoverTween = new();

        private static bool _needFadingStart;
        private static bool _needFadingEnd;

        private static bool _needCoverFadingEnd;

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

        #region Sound

        internal static void PlaySound(object sound, bool loop = false, string channelOverrideName = null)
        {
            if (Instance == null)
                return;
            SoundClipFixed clip;
            switch (sound)
            {
                case string id:
                    var data = Database.GetRecord<Sound>(id);
                    if (data != null)
                    {
                        var s = data.Sample;
                        var channel = channelOverrideName.IsNullOrWhitespace() ? s.Channel.Name : channelOverrideName;
                        clip = new SoundClipFixed(s, loop, channel);
                        break;
                    }

                    Debug.LogError($"[AudioPlayer] Unknown sound ID: {id}");
                    return;
                case Sound s:
                    clip = new SoundClipFixed(s.Sample, loop, channelOverrideName);
                    break;
                case AudioClip a:
                    clip = new SoundClipFixed(a, loop, channelOverrideName);
                    break;
                default:
                    Debug.LogError($"[AudioPlayer] Unknown sound type: {sound.GetType().Name}");
                    return;
            }

            Instance.pool.AddItem(clip);
            if (!loop)
                TimeController.Call(() => Instance.pool.RemoveItem(clip), clip.GetDuration(), clip);
        }

        internal static void StopAllSounds(string channel = null)
        {
            if (channel == null)
                Instance.pool.Clear();
            else
                Instance.pool.RemoveByCallback((item) => item is SoundClipFixed clip && clip.Channel?.Name == channel);
        }

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
        internal static void PlayMusic(object music,
            bool fadingStart = true,
            bool fadingEnd = true,
            string overrideChannel = null)
        {
            _needFadingStart = fadingStart;

            var clip = GetMusicClip(music, overrideChannel);
            Instance._music = clip.GetClip();

            if (!FadeForMusic(() =>
                {
                    Instance.musicPlayer.Stop();
                    PlayMusic(music, fadingStart, fadingEnd);
                }))
                return;

            _needFadingEnd = fadingEnd;
            FadeTween.Kill();
            if (fadingStart)
                FadeTween.Set(() => 0f, Instance.musicPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .Run();
            else
                Instance.musicPlayer.SetVolumeMultiplier(1);

            Instance.musicPlayer.Play(clip);
        }

        /// <summary>
        /// Остановка музыки
        /// </summary>
        internal static void StopMusic()
        {
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
                FadeTween.Kill();
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
            else if (Instance.musicPlayer.IsPlay() && Instance.musicPlayer.GetVolumeMultiplier() > 0)
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
        /// Запуск ситуативной музыки
        /// </summary>
        /// <param name="music"></param>
        /// <param name="fadingStart"></param>
        /// <param name="fadingEnd"></param>
        /// <param name="defaultChannel"></param>
        internal static void PlayCoverMusic(object music, bool fadingStart = true, bool fadingEnd = true,
            string defaultChannel = null)
        {
            var clip = GetMusicClip(music, defaultChannel);

            Instance._coverMusic = clip.GetClip();

            if (!FadeForCoverMusic(() => PlayCoverMusic(clip, fadingStart, fadingEnd)))
                return;

            _needCoverFadingEnd = fadingEnd;
            FadeTween.Kill();
            if (fadingStart)
                FadeCoverTween.Set(() => 0f, Instance.musicCoverPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                    .SetEase(EaseType.Linear)
                    .Run();
            else
                Instance.musicCoverPlayer.SetVolumeMultiplier(1);

            Instance.musicCoverPlayer.Play(clip);
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
            FadeTween.Kill();
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
            if (Instance.musicPlayer.IsPlay())
            {
                FadeTween.Kill();
                if (_needFadingStart)
                    FadeTween.Set(() => 0f, Instance.musicPlayer.SetVolumeMultiplier, 1f, Instance.musicFadeTime)
                        .SetEase(EaseType.Linear)
                        .Run();
                else
                    Instance.musicPlayer.SetVolumeMultiplier(1);
            }
        }

        #endregion

        private static SoundClip GetMusicClip(object music, string overrideChannel)
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