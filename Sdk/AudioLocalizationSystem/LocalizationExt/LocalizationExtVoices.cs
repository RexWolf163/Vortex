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
        /// Узнать текущую локаль канала 
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentChannelLanguage(LocaleChannels channel)
        {
            if (channel == LocaleChannels.Default)
                return Driver.GetDefaultLanguage(); //дефолтный язык
            if (CurrentVoiceLanguage.IsNullOrWhitespace())
                SetCurrentChannelLanguage(
                    ChDriver?.GetChannelLanguage((byte)channel)
                    ?? Driver.GetDefaultLanguage(), channel); //дефолтный язык
            return CurrentVoiceLanguage;
        }

        /// <summary>
        /// Установить локаль для канала локализации 
        /// </summary>
        /// <param name="language"></param>
        /// <param name="channel"></param>
        public static void SetCurrentChannelLanguage(string language, LocaleChannels channel)
        {
            _currentVoiceLanguage = language;
            ChDriver?.SetChannelLanguage((byte)channel, language)
                .Forget(ex => Log.Print(LogLevel.Error, ex.Message, "Localization"));
        }
    }
}