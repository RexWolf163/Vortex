using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.LocalizationSystem.Bus;

namespace Vortex.Sdk.AudioLocalizationSystemV2.Model
{
    /// <summary>
    /// Данные локализации для ключа локали
    /// Хранит карту ассоциаций звуковых ассетов относительно разных языков для указанного ключа локализации
    ///
    /// Клип реплики — собственный ассет записи за <see cref="AssetHandle{T}"/> (прямая ссылка или ленивая
    /// подгрузка), а не запись Sound в БД. Хэндлы — независимые deep-copy от пресета (базовый CopyFrom),
    /// загрузку/выгрузку ведёт <c>AudioLocalizationController</c> на время проигрывания.
    /// </summary>
    public class AudioLocaleData : Record
    {
        public string TextGuid { get; private set; }

        /// <summary>
        /// Словарь-индекс
        /// Язык - хэндл звукового ассета
        /// </summary>
        public Dictionary<string, AssetHandle<AudioClip>> Voices { get; private set; }

        /// <summary>
        /// Аудиоканал воспроизведения реплики
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Возвращает хэндл звукового ассета для текущего языка канала Voice.
        /// <c>null</c> — для языка нет озвучки.
        /// </summary>
        /// <returns></returns>
        public AssetHandle<AudioClip> GetLocale()
        {
            var currentLanguage = Localization.GetCurrentChannelLanguage(LocaleChannels.Voice);
            return Voices.GetValueOrDefault(currentLanguage);
        }

        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
        }
    }
}
