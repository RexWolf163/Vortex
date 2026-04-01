using System;
using System.Collections.Generic;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.AudioSystem.Bus
{
    public partial class AudioController : SystemController<AudioController, IDriver>
    {
        #region Params

        private static readonly Dictionary<string, IAudioSample> IndexSound = new();

        private static readonly Dictionary<string, IAudioSample> IndexMusic = new();

        public static AudioSettings Settings { get; } = new();

        #endregion

        #region Events

        /// <summary>
        /// Были изменены настройки звука
        /// </summary>
        public static event Action OnSettingsChanged;

        #endregion

        protected override void OnDriverConnect()
        {
            Driver.SetLinks(IndexSound, IndexMusic, Settings);
        }

        protected override void OnDriverDisconnect()
        {
        }

        #region AudioSettings

        /// <summary>
        /// Включить/выключить звуки
        /// </summary>
        /// <param name="soundOn"></param>
        public static void SetMasterState(bool soundOn)
        {
            Settings.MasterOn = soundOn;
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Включить/выключить звуки
        /// </summary>
        /// <param name="soundOn"></param>
        public static void SetSoundState(bool soundOn)
        {
            Settings.SoundOn = soundOn;
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Включить/выключить музыку
        /// </summary>
        /// <param name="musicOn"></param>
        public static void SetMusicState(bool musicOn)
        {
            Settings.MusicOn = musicOn;
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Изменить громкость звуков
        /// </summary>
        /// <param name="value">значение от 0 до 1</param>
        public static void SetSoundVolume(float value)
        {
            Settings.SoundVolume = value;
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Изменить громкость музыки
        /// </summary>
        /// <param name="value">значение от 0 до 1</param>
        public static void SetMusicVolume(float value)
        {
            Settings.MusicVolume = value;
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Изменить громкость всего звука 
        /// </summary>
        /// <param name="value">значение от 0 до 1</param>
        public static void SetMasterVolume(float value)
        {
            Settings.MasterVolume = value;
            OnSettingsChanged?.Invoke();
        }

        #endregion

        #region AudioControl

        public static void PlaySound(object sound, bool loop = false) => Driver.PlaySound(sound, loop);

        public static void StopAllSounds(string channel = null) => Driver.StopAllSounds(channel);

        public static void PlayMusic(object audioClip, bool fadingStart = true, bool fadingEnd = true) =>
            Driver.PlayMusic(audioClip, fadingStart, fadingEnd);

        public static void StopMusic() => Driver.StopMusic();

        public static void PlayCoverMusic(object audioClip, bool fadingStart = true, bool fadingEnd = true) =>
            Driver.PlayCoverMusic(audioClip, fadingStart, fadingEnd);

        public static void StopCoverMusic() => Driver.StopCoverMusic();

        #endregion

        /// <summary>
        /// Получить сэмпл звука
        /// </summary>
        /// <returns></returns>
        public static IAudioSample GetSample(string guid)
        {
            if (IndexSound.TryGetValue(guid, out var soundSample))
                return soundSample;
            if (IndexMusic.TryGetValue(guid, out var musicSample))
                return musicSample;

            Log.Print(new LogData(LogLevel.Error, $"Sample #{guid} not found.", "AudioPlayer"));
            return null;
        }
    }
}