using System.Collections.Generic;
using System.Linq;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using NotImplementedException = System.NotImplementedException;

namespace Vortex.Core.AudioSystem.Bus
{
    /// <summary>
    /// Расширение шины на управление каналами звуков
    ///
    /// Канал работает как мультипликатор для громкости звука или музыки
    /// </summary>
    public partial class AudioController
    {
        /// <summary>
        /// Возвращает список ключей каналов
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<string> GetChannelsList() => Settings.Channels.Keys.ToList();

        /// <summary>
        /// Возвращает список каналов
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<AudioChannel> GetChannels() => Settings.Channels.Values.ToList();

        /// <summary>
        /// Возвращает канал с указанным ключом
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public static AudioChannel GetChannel(string channelName) =>
            channelName.IsNullOrWhitespace()
                ? null
                : Settings.Channels.GetValueOrDefault(channelName, null);

        /// <summary>
        /// Возвращает громкость установленную в канале
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="baseValue">Дефолтное знаечние - вернет если канал не найден</param>
        /// <returns></returns>
        public static float GetChVolume(string channelId, float baseValue = 1f)
        {
            if (channelId.IsNullOrWhitespace() || !Settings.Channels.TryGetValue(channelId, out var channel))
                return baseValue;
            return channel.Volume;
        }

        /// <summary>
        /// Задает громкость для канала
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="value"></param>
        public static void SetChVolume(string channelId, float value)
        {
            if (!Settings.Channels.TryGetValue(channelId, out var channel)) return;
            channel.Volume = value;
            channel.CallOnUpdate();
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Возвращает скалькулированную громкость музыки для канала
        /// Если канал пуст или не найден - множитель канала будет считаться как 1 
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public static float GetMusicVolume(string channelName = null) =>
            Settings.MasterVolume * Settings.MusicVolume * GetChVolume(channelName);

        /// <summary>
        /// Возвращает скалькулированную громкость звуков для канала
        /// Если канал пуст или не найден - множитель канала будет считаться как 1 
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public static float GetSoundVolume(string channelName = null) =>
            Settings.MasterVolume * Settings.SoundVolume * GetChVolume(channelName);

        public static bool GetMusicOn(string channelName = null) =>
            Settings.MasterOn && Settings.MusicOn && !(GetChannel(channelName)?.Mute ?? false);

        public static bool GetSoundOn(string channelName = null) =>
            Settings.MasterOn && Settings.SoundOn && !(GetChannel(channelName)?.Mute ?? false);
    }
}