using System;
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
        /// Значение текущей локали канала
        /// </summary>
        private static string[] _currentChannelLanguage;

        /// <summary>
        /// Значение текущей локали канала
        /// </summary>
        private static string[] CurrentChannelLanguage =>
            _currentChannelLanguage ??= new string[Enum.GetValues(typeof(LocaleChannels)).Length];

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
            var ch = (byte)channel;
            if (CurrentChannelLanguage[ch].IsNullOrWhitespace())
                SetCurrentChannelLanguage(
                    ChDriver?.GetChannelLanguage(ch)
                    ?? Driver.GetDefaultLanguage(), channel); //дефолтный язык
            return CurrentChannelLanguage[ch];
        }

        /// <summary>
        /// Установить локаль для канала локализации 
        /// </summary>
        /// <param name="language"></param>
        /// <param name="channel"></param>
        public static void SetCurrentChannelLanguage(string language, LocaleChannels channel)
        {
            var ch = (byte)channel;
            CurrentChannelLanguage[ch] = language;
            ChDriver?.SetChannelLanguage(ch, language)
                .Forget(ex => Log.Print(LogLevel.Error, ex.Message, "Localization"));
        }
    }
}