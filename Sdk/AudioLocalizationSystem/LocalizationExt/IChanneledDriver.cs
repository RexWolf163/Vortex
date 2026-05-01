using Cysharp.Threading.Tasks;

namespace Vortex.Core.LocalizationSystem.Bus
{
    public interface IChanneledDriver
    {
        /// <summary>
        /// Сохранить настройку языка для указанного канала
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="language"></param>
        public UniTask SetChannelLanguage(byte channel, string language);

        /// <summary>
        /// Загрузить настройку языка для указанного канала
        /// </summary>
        /// <returns></returns>
        public string GetChannelLanguage(byte channel);
    }
}