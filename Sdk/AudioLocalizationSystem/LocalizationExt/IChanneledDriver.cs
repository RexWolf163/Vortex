using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.LocalizationSystem.Bus
{
    public interface IChanneledDriver
    {
        /// <summary>
        /// Событие смены языка локали по каналу
        /// </summary>
        public event Action<byte> OnLocalizationInChannelChanged;

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

        /// <summary>
        /// Собрать индекс-копию (ключ → перевод) для указанного языка из данных драйвера.
        /// Синхронно: все языки уже в памяти пресета, IO нет. Хранение/кеш пакетов — на стороне
        /// <see cref="Localization"/> (партиал каналов); драйвер только строит копию по запросу.
        /// Отсутствие перевода ключа в запрошенном языке → падение на дефолтный язык (не FailFast:
        /// частичная локаль ожидаема на предрелизе, «левый» язык виден на тесте).
        /// </summary>
        /// <param name="language">Ключ языка (как в <c>GetChannelLanguage</c>/<c>GetLanguages</c>).</param>
        public Dictionary<string, string> GetLanguagePack(string language);
    }
}