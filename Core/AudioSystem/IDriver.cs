using System.Collections.Generic;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.AudioSystem
{
    public interface IDriver : ISystemDriver
    {
        /// <summary>
        /// Передача линка на реестр
        /// </summary>
        /// <param name="indexSound">Ссылка на реестр сэмплов звуков</param>
        /// <param name="indexMusic">Ссылка на реестр сэмплов музыки</param>
        /// <param name="settings">Ссылка на базовые настройки воспроизведения</param>
        public void SetLinks(Dictionary<string, IAudioSample> indexSound,
            Dictionary<string, IAudioSample> indexMusic,
            AudioSettings settings);

        /// <summary>
        /// Воспроизведение звука
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="loop"></param>
        /// <param name="defaultChannel">
        /// дефолтный канал для воспроизведения. Если не установлен, то в первый доступный канал
        /// в списке, если не задан канала в модели звука
        /// </param>
        public void PlaySound(object sound, bool loop = false, string defaultChannel = null);

        /// <summary>
        /// Остановка всех звуков.
        /// Если указан канал - то остановка всех звуков конкретного канала
        /// </summary>
        /// <param name="channel">канал для остановки звуков</param>
        public void StopAllSounds(string channel = null);

        /// <summary>
        /// Воспроизведение основной музыки
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="fadingStart">Плавное начало</param>
        /// <param name="fadingEnd">Плавное затухание</param>
        /// <param name="defaultChannel">
        /// дефолтный канал для воспроизведения. Если не установлен, то в первый доступный канал
        /// в списке, если не задан канала в модели звука
        /// </param>
        public void PlayMusic(object audioClip, bool fadingStart = true, bool fadingEnd = true,
            string defaultChannel = null);

        /// <summary>
        /// Остановка основной музыки
        /// </summary>
        public void StopMusic();

        /// <summary>
        /// Воспроизведение ситуативной музыки
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="fadingStart">Плавное начало</param>
        /// <param name="fadingEnd">Плавное затухание</param>
        /// <param name="defaultChannel">
        /// дефолтный канал для воспроизведения. Если не установлен, то в первый доступный канал
        /// в списке, если не задан канала в модели звука
        /// </param>
        public void PlayCoverMusic(object audioClip, bool fadingStart = true, bool fadingEnd = true,
            string defaultChannel = null);

        /// <summary>
        /// Остановка ситуативной музыки
        /// </summary>
        public void StopCoverMusic();
    }
}