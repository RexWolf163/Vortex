using System;
using System.Collections.Generic;
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
        /// Индекс-копии текста по каналам (ключ → перевод в языке канала). Держим ТОЛЬКО когда язык канала
        /// расходится с основным; при совпадении делегируем в базовый <see cref="Index"/> и пак не держим.
        /// Пак строит драйвер (<see cref="IChanneledDriver.GetLanguagePack"/>), храним/кешируем здесь.
        /// </summary>
        private static Dictionary<string, string>[] _channelIndex;

        /// <summary>
        /// Язык, под который собран соответствующий <see cref="_channelIndex"/> — для живой инвалидации
        /// (сменился язык канала → пак пересобираем без подписок).
        /// </summary>
        private static string[] _channelIndexLang;

        private static Dictionary<string, string>[] ChannelIndex =>
            _channelIndex ??= new Dictionary<string, string>[Enum.GetValues(typeof(LocaleChannels)).Length];

        private static string[] ChannelIndexLang =>
            _channelIndexLang ??= new string[Enum.GetValues(typeof(LocaleChannels)).Length];

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

        /// <summary>
        /// Перевод ключа в языке ВЫБРАННОГО канала (а не основного). Правила:
        /// <list type="bullet">
        /// <item>канал <see cref="LocaleChannels.Default"/>, драйвер не канальный, либо язык канала совпал с
        /// основным → базовый <see cref="GetTranslate"/> (тот же индекс, что и <c>Translate()</c>);</item>
        /// <item>иначе — из индекс-копии канала (лениво собирается драйвером и кешируется здесь).</item>
        /// </list>
        /// Совпадение с основным и смена языка канала отслеживаются живьём — подписок не требует.
        /// </summary>
        public static string GetChannelTranslate(string key, LocaleChannels channel)
        {
            if (channel == LocaleChannels.Default)
                return GetTranslate(key);

            var lang = GetCurrentChannelLanguage(channel);

            // Канал не расходится с основным (или каналов нет) → индекс-копию не держим, идём в базовый.
            if (ChDriver == null || lang == GetCurrentLanguage())
            {
                ReleaseChannelPack(channel);
                return GetTranslate(key);
            }

            var pack = GetOrBuildChannelPack(channel, lang);
            return pack != null && pack.TryGetValue(key, out var value) ? value : $"##!{key}!##";
        }

        /// <summary>
        /// Есть ли перевод ключа в языке канала (для мест, где промах не должен подставлять маркер).
        /// </summary>
        public static bool HasChannelTranslate(string key, LocaleChannels channel)
        {
            if (channel == LocaleChannels.Default)
                return HasTranslate(key);
            var lang = GetCurrentChannelLanguage(channel);
            if (ChDriver == null || lang == GetCurrentLanguage())
                return HasTranslate(key);
            return GetOrBuildChannelPack(channel, lang)?.ContainsKey(key) ?? false;
        }

        private static Dictionary<string, string> GetOrBuildChannelPack(LocaleChannels channel, string lang)
        {
            var ch = (byte)channel;
            if (ChannelIndex[ch] == null || ChannelIndexLang[ch] != lang)
            {
                ChannelIndex[ch] = ChDriver.GetLanguagePack(lang);
                ChannelIndexLang[ch] = lang;
            }

            return ChannelIndex[ch];
        }

        // Отпускаем индекс-копию канала (совпал с основным / нет канального драйвера) — держим инвариант
        // «пак существует ⟺ язык канала ≠ основного» и не течём памятью после схлопывания языков.
        private static void ReleaseChannelPack(LocaleChannels channel)
        {
            var ch = (byte)channel;
            if (ChannelIndex[ch] == null)
                return;
            ChannelIndex[ch] = null;
            ChannelIndexLang[ch] = null;
        }
    }
}