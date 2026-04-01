using Cysharp.Threading.Tasks;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.LocalizationSystem.Bus
{
    /// <summary>
    /// Расширение локализации для работы с наниновелл
    /// </summary>
    public partial class Localization
    {
        /// <summary>
        /// Значение текущей локали голоса
        /// </summary>
        private static string _currentVoiceLanguage;

        /// <summary>
        /// Значение текущей локали голоса
        /// </summary>
        private static string CurrentVoiceLanguage => _currentVoiceLanguage;

        /// <summary>
        /// Значение текущей локали диалогов
        /// </summary>
        private static string _currentDialogueLanguage;

        /// <summary>
        /// Значение текущей локали диалогов
        /// </summary>
        private static string CurrentDialogueLanguage => _currentDialogueLanguage;

        /// <summary>
        /// Обработка драйвера как IChanneledDriver
        /// для оперирования каналами локализации
        /// </summary>
        private static IChanneledDriver ChDriver => Driver as IChanneledDriver;

        /// <summary>
        /// Узнать текущую локаль голоса 
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentVoiceLanguage()
        {
            if (CurrentVoiceLanguage.IsNullOrWhitespace())
                SetCurrentVoiceLanguage(
                    ChDriver?.GetChannelLanguage((byte)LocaleChannels.Voice)
                    ?? Driver.GetDefaultLanguage()); //дефолтный язык
            return CurrentVoiceLanguage;
        }

        /// <summary>
        /// Узнать текущую локаль диалогов 
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentDialogueLanguage()
        {
            if (CurrentDialogueLanguage.IsNullOrWhitespace())
                SetCurrentDialogueLanguage(
                    ChDriver?.GetChannelLanguage((byte)LocaleChannels.Dialogue)
                    ?? Driver.GetDefaultLanguage()); //дефолтный язык
            return CurrentDialogueLanguage;
        }

        public static void SetCurrentVoiceLanguage(string language)
        {
            _currentVoiceLanguage = language;
            ChDriver?.SetChannelLanguage((byte)LocaleChannels.Voice, language)
                .Forget(ex => Log.Print(LogLevel.Error, ex.Message, "[Localization]"));
        }

        public static void SetCurrentDialogueLanguage(string language)
        {
            _currentDialogueLanguage = language;
            ChDriver?.SetChannelLanguage((byte)LocaleChannels.Dialogue, language)
                .Forget(ex => Log.Print(LogLevel.Error, ex.Message, "[Localization]"));
        }
    }
}