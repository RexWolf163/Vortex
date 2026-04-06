using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.AudioSystem.Bus;
using Vortex.NaniExtensions.Core;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.AudioSystem.Presets;

namespace Vortex.NaniExtensions.AudioSystem
{
    /// <summary>
    /// Контроллер обеспечивает трансляцию настроек громкости в наниновел
    /// Звуки не выставляются в движок при изменении настроек, если игра уже запущена,
    /// так как локальная логика может конфликтовать.
    /// Вместо этого предоставляются методы для получения целевой громкости.
    /// Но если игра в режиме Off, то изменение настроек сразу проецируется в Нани
    /// </summary>
    public static class NaniVortexAudioConnector
    {
        private static string _bgmChannel;
        private static string _sfxChannel;
        private static string _voiceChannel;
        private static string _voiceCutsceneChannel;
        private static bool _cutsceneMode;

        private static bool _wasStarted;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            App.OnStart -= Init;
            App.OnStart += Init;
            App.OnExit -= Dispose;
            App.OnExit += Dispose;
        }

        private static void Dispose()
        {
            App.OnStart -= Init;
            App.OnExit -= Dispose;
            AudioController.OnSettingsChanged -= OnSettingsChanged;
        }

        private static void Init()
        {
            if (_wasStarted) return;
            _wasStarted = true;
            var channelConfigs = Resources.LoadAll<AudioChannelsConfig>("");
            var channelConfig = channelConfigs.Length > 0 ? channelConfigs[0] : null;
            if (channelConfig == null)
            {
                Debug.LogError("[NaniVortexAudioConnector] Audio channels config not found.");
                return;
            }

            _bgmChannel = channelConfig.GetNaniBgmChannel();
            _sfxChannel = channelConfig.GetSfxChannel();
            _voiceChannel = channelConfig.GetVoiceChannel();
            _voiceCutsceneChannel = channelConfig.GetVoiceCutsceneChannel();

            NaniWrapper.AudioManager.BgmVolume = GetNaniBgmVolume();
            NaniWrapper.AudioManager.SfxVolume = GetNaniSfxVolume();
            NaniWrapper.AudioManager.VoiceVolume = GetNaniVoiceVolume();

            AudioController.OnSettingsChanged += OnSettingsChanged;
        }

        private static void OnSettingsChanged()
        {
            if (GameController.GetState() != GameStates.Off)
                return;
            NaniWrapper.AudioManager.BgmVolume = GetNaniBgmVolume();
            NaniWrapper.AudioManager.SfxVolume = GetNaniSfxVolume();
            NaniWrapper.AudioManager.VoiceVolume = GetNaniVoiceVolume();
        }

        public static float GetNaniBgmVolume() =>
            AudioController.GetSoundOn(_bgmChannel) ? AudioController.GetSoundVolume(_bgmChannel) : 0;

        public static float GetNaniSfxVolume() =>
            AudioController.GetSoundOn(_sfxChannel) ? AudioController.GetSoundVolume(_sfxChannel) : 0;

        public static float GetNaniVoiceVolume()
        {
            if (_cutsceneMode)
                return AudioController.GetSoundOn(_voiceCutsceneChannel)
                    ? AudioController.GetSoundVolume(_voiceCutsceneChannel)
                    : 0;
            return AudioController.GetSoundOn(_voiceChannel)
                ? AudioController.GetSoundVolume(_voiceChannel)
                : 0;
        }

        public static void SetCutsceneMode(bool mode) => _cutsceneMode = mode;
    }
}