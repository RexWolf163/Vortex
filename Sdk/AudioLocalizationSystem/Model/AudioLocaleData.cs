using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Sdk.AudioLocalizationSystem.Model
{
    /// <summary>
    /// Данные локализации для ключа локали
    /// Хранит карту ассоциаций звуковых ассетов относительно разных языков для указанного ключа локализации
    /// </summary>
    public class AudioLocaleData : Record
    {
        public string TextGuid { get; private set; }

        /// <summary>
        /// Словарь-индекс
        /// Язык - guid звукового ассета в БД
        /// </summary>
        public Dictionary<string, string> Voices { get; private set; }

        /// <summary>
        /// Значение языка для которого кеширован звук
        /// </summary>
        private string _cachedLang;

        /// <summary>
        /// Кешированное значение звука для воспроизведения
        /// </summary>
        private SoundClipFixed _cachedSound;

        /// <summary>
        /// Возвращает модель аудио данных для текущего языка
        /// </summary>
        /// <returns></returns>
        public Sound GetLocale()
        {
            var currentLanguage = Localization.GetCurrentVoiceLanguage();
            if (!Voices.TryGetValue(currentLanguage, out var voice))
            {
                _cachedSound = null;
                _cachedLang = currentLanguage;
                return null;
            }

            var sound = Database.GetRecord<Sound>(voice);
            if (_cachedLang == currentLanguage)
                return sound;
            _cachedSound = sound == null ? null : new SoundClipFixed(sound.Sample);
            _cachedLang = currentLanguage;
            return sound;
        }

        /// <summary>
        /// Возвращает SoundClipFixed уже готовый для проигрывания в аудио системе
        /// </summary>
        /// <returns></returns>
        public SoundClipFixed GetSoundClip()
        {
            var currentLanguage = Localization.GetCurrentVoiceLanguage();
            if (_cachedLang != currentLanguage)
                GetLocale();
            return _cachedSound;
        }

        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
        }
    }
}