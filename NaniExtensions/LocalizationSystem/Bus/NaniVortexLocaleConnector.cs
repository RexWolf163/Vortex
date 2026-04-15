using Naninovel;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.NaniExtensions.Core;

namespace Vortex.NaniExtensions.LocalizationSystem.Bus
{
    /// <summary>
    /// Коннектор Нани-Vortex для передачи сигнала о смене локализации
    /// </summary>
    public static class NaniVortexLocaleConnector
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            App.OnStart -= Init;
            App.OnStart += Init;
        }

        private static void Init()
        {
            App.OnStart -= Init;

            Localization.OnLocalizationChanged -= SetNaniDialogueLocale;
            Localization.OnLocalizationChanged += SetNaniDialogueLocale;

            SetNaniDialogueLocale();
            //SetNaniVoiceLocale(); Установка локали диалога запустит каскад
        }

        private static void Exit()
        {
            Localization.OnLocalizationChanged -= SetNaniDialogueLocale;
        }

        private static void SetNaniDialogueLocale()
        {
            if (App.GetState() == AppStates.Stopping)
            {
                Exit();
                return;
            }

            NaniWrapper.L10N.SelectLocale(Localization.GetCurrentDialogueLanguage());
            NaniWrapper.StateManager.SaveGlobal();
            SetNaniVoiceLocale();
        }

        private static void SetNaniVoiceLocale()
        {
            if (App.GetState() == AppStates.Stopping)
            {
                Exit();
                return;
            }

            var voiceLoader = (LocalizableResourceLoader<AudioClip>)NaniWrapper.AudioManager.VoiceLoader;
            if (voiceLoader == null) return;
            voiceLoader.OverrideLocale = Localization.GetCurrentVoiceLanguage();
            NaniWrapper.StateManager.SaveGlobal();
        }
    }
}