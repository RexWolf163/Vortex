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
        /// Узнать текущую локаль канала.
        /// Канал <see cref="LocaleChannels.Default"/> — синоним базового <see cref="GetCurrentLanguage"/>;
        /// собственного состояния не имеет, чтобы не расходиться с <c>_currentLanguage</c> базовой партиальной.
        /// </summary>
        public static string GetCurrentChannelLanguage(LocaleChannels channel)
        {
            if (channel == LocaleChannels.Default)
                return GetCurrentLanguage();
            var ch = (byte)channel;
            if (CurrentChannelLanguage[ch].IsNullOrWhitespace())
                SetCurrentChannelLanguage(
                    ChDriver?.GetChannelLanguage(ch)
                    ?? Driver.GetDefaultLanguage(), channel); //дефолтный язык
            return CurrentChannelLanguage[ch];
        }

        /// <summary>
        /// Установить локаль для канала локализации.
        /// Канал <see cref="LocaleChannels.Default"/> делегируется в базовый <see cref="SetCurrentLanguage"/>,
        /// чтобы синхронизировать <c>_currentLanguage</c> базовой партиальной с состоянием UI/драйвера.
        /// </summary>
        /// <param name="language"></param>
        /// <param name="channel"></param>
        public static void SetCurrentChannelLanguage(string language, LocaleChannels channel)
        {
            if (channel == LocaleChannels.Default)
            {
                SetCurrentLanguage(language);
                return;
            }

            var ch = (byte)channel;
            CurrentChannelLanguage[ch] = language;
            ChDriver?.SetChannelLanguage(ch, language)
                .Forget(ex => Log.Print(LogLevel.Error, ex.Message, "Localization"));
        }
    }
}