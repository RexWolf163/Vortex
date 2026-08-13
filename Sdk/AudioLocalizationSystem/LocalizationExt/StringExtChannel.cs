using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Core.LocalizationSystem.Bus
{
    /// <summary>
    /// Канальный аналог <see cref="Vortex.Core.LocalizationSystem.StringExt.Translate"/>: перевод ключа
    /// в языке ВЫБРАННОГО канала локализации. Живёт в SDK-пакете аудиолокализации (базовый StringExt —
    /// в Core-сборке, её не трогаем).
    /// </summary>
    public static class StringExtChannel
    {
        /// <summary>
        /// Ассоциация с ключом в языке указанного канала. Пустой ключ → "".
        /// Канал <see cref="LocaleChannels.Default"/> эквивалентен обычному <c>Translate()</c>.
        /// </summary>
        public static string TranslateChannel(this string key, LocaleChannels channel) =>
            key.IsNullOrWhitespace() ? "" : Localization.GetChannelTranslate(key, channel);

        /// <summary>
        /// Как <see cref="TranslateChannel"/>, но при отсутствии перевода в языке канала возвращает ключ
        /// без изменений (для спец-случаев, где маркер промаха нежелателен).
        /// </summary>
        public static string TryTranslateChannel(this string key, LocaleChannels channel) =>
            Localization.HasChannelTranslate(key, channel) ? Localization.GetChannelTranslate(key, channel) : key;
    }
}
