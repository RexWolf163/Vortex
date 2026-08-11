using Naninovel;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.NaniExtensions.Core;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.AudioSystem.Presets;
using AudioController = Vortex.Core.AudioSystem.Bus.AudioController;

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
            Engine.OnInitializationFinished -= SyncNaniVolumes;
        }

        private static void Init()
        {
            if (_wasStarted) return;
            _wasStarted = true;
            var channelConfigs = Resources.LoadAll<AudioChannelsConfig>("");
            if (channelConfigs.Length == 0)
            {
                Debug.LogError("[NaniVortexAudioConnector] AudioChannelsConfig not found in Resources.");
                return;
            }

            if (channelConfigs.Length > 1)
                Debug.LogError(
                    $"[NaniVortexAudioConnector] Found {channelConfigs.Length} AudioChannelsConfig assets, expected exactly 1. Using '{channelConfigs[0].name}'.");

            var channelConfig = channelConfigs[0];

            _bgmChannel = channelConfig.GetNaniBgmChannel();
            _sfxChannel = channelConfig.GetSfxChannel();
            _voiceChannel = channelConfig.GetVoiceChannel();
            _voiceCutsceneChannel = channelConfig.GetVoiceCutsceneChannel();

            SyncNaniVolumes();

            AudioController.OnSettingsChanged += OnSettingsChanged;
            // Naninovel LoadGlobal (после инициализации движка) применяет СВОИ сохранённые в global state
            // громкости и перебивает пуш выше — из-за чего сброшенные значения «слетают» на старте. Пере-пушим
            // Vortex (источник истины) ПОСЛЕ каждой инициализации движка.
            Engine.OnInitializationFinished -= SyncNaniVolumes;
            Engine.OnInitializationFinished += SyncNaniVolumes;
        }

        private static void OnSettingsChanged()
        {
            if (GameController.GetState() != GameStates.Off)
                return;
            SyncNaniVolumes();
        }

        // Пуш громкостей Vortex→Naninovel (Vortex — источник истины).
        private static void SyncNaniVolumes()
        {
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