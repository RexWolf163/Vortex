using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Sdk.AudioLocalizationSystem.Model;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.LocalizationSystem;

namespace Vortex.Sdk.AudioLocalizationSystem.Preset
{
    [CreateAssetMenu(fileName = "AudioLocaleData", menuName = "Database/AudioLocaleData")]
    public class AudioLocaleDataPreset : RecordPreset<AudioLocaleData>
    {
        [Serializable, ClassLabel("$Label")]
        private struct LangGroup
        {
            [Language] public string language;

            [DbRecord(typeof(Sound))] public string audio;

#if UNITY_EDITOR
            private string Label()
            {
                if (audio.IsNullOrWhitespace())
                    return $"Language: {language} => [NULL]";
                var record = Database.GetRecord<Sound>(audio);
                return $"Language: {language} => {record.Sample.GetClip().name}";
            }
#endif
        }

        [SerializeField, LocalizationKey] private string textGuid;

        /// <summary>
        /// Ключ локализации строки которую нужно озвучить
        /// </summary>
        public string TextGuid => textGuid;

        [SerializeField] private List<LangGroup> voices;

        /// <summary>
        /// Индекс Язык=> GUID звука
        /// </summary>
        public Dictionary<string, string> Voices => voices.ToDictionary(d => d.language, d => d.audio);

#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.Singleton;

        /// <summary>
        /// Подразумевается что ключ локали будет начинатсья с ключа персонажа произносящего фразу
        /// с точкой, отделяющей от остального ключа
        /// </summary>
        [FoldoutGroup("Грубый инструментарий массовой замены")]
        [Button("Update Voices Array", ButtonSizes.Medium)]
        private void UpdateVoices()
        {
            if (voices != null && voices.Count > 0)
            {
                var allSounds = Database.GetRecords<Sound>().ToList();
                for (int i = 0; i < voices.Count; i++)
                {
                    var voice = voices[i];
                    if (string.IsNullOrEmpty(voice.audio)) continue;

                    var oldRecord = Database.GetRecord<Sound>(voice.audio);
                    if (oldRecord == null || string.IsNullOrEmpty(oldRecord.Name)) continue;

                    var dotIndex = oldRecord.Name.IndexOf('.');
                    if (dotIndex < 0) continue;

                    var newName = $"{TextGuid}.{voice.language}";

                    var newRecord = allSounds.FirstOrDefault(s =>
                        string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase));
                    if (newRecord == null) continue;

                    voice.audio = newRecord.GuidPreset;
                    voices[i] = voice;
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ShowInInspector, HorizontalGroup("Грубый инструментарий массовой замены/NewChar")]
        private string charName;

        /// <summary>
        /// Подразумевается что ключ локали будет начинатсья с ключа персонажа произносящего фразу
        /// с точкой, отделяющей от остального ключа
        /// </summary>
        [Button("Замена первой части ключа", ButtonSizes.Medium),
         HorizontalGroup("Грубый инструментарий массовой замены/NewChar")]
        private void ChangeChar()
        {
            if (string.IsNullOrEmpty(charName)) return;

            // 1. textGuid ← ключ локализации "{charName}.{Name}" (без учёта регистра)
            var dotIndex = textGuid.IndexOf('.');
            if (dotIndex < 0) return;
            var oldSuffix = textGuid.Substring(dotIndex + 1);
            var candidateTextKey = $"{charName}.{oldSuffix}";
            var matchedTextKey = Localization.GetLocalizationKeys().FirstOrDefault(k =>
                string.Equals(k, candidateTextKey, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchedTextKey))
                textGuid = matchedTextKey;
        }

        [Language, ShowInInspector, HorizontalGroup("Грубый инструментарий массовой замены/NewLang")]
        private string _newLanguage;

        /// <summary>
        /// Подразумевается что в ключе есть участок с указанием языка через его id в виде ".{lang}."
        /// Например Hero.Text1.ru.2
        /// </summary>
        [Button("Скопировать первую запись под другой язык", ButtonSizes.Medium),
         HorizontalGroup("Грубый инструментарий массовой замены/NewLang")]
        private void AddLanguageEntry()
        {
            if (string.IsNullOrEmpty(_newLanguage)) return;

            voices ??= new List<LangGroup>();

            // Запись для этого языка уже есть — ничего не делаем
            if (voices.Any(v => v.language == _newLanguage)) return;

            // Если voices пустой — первая запись без шаблона
            if (voices.Count == 0)
            {
                voices.Add(new LangGroup { language = _newLanguage, audio = string.Empty });
                UnityEditor.EditorUtility.SetDirty(this);
                return;
            }

            // Берём первый элемент как образец
            var template = voices[0];
            var newAudio = template.audio ?? string.Empty;

            var oldAudioRec = Database.GetRecord<Sound>(newAudio);
            if (oldAudioRec == null)
                return;
            var newAudioName = oldAudioRec.Name;

            // Меняем .{templateLang}. на .{newLanguage}. в строке audio образца
            if (!string.IsNullOrEmpty(template.audio) && !string.IsNullOrEmpty(template.language))
            {
                newAudioName = newAudioName.Replace($".{template.language}", $".{_newLanguage}");
            }

            newAudio = Database.GetRecords<Sound>().First(s => s.Name == newAudioName).GuidPreset;

            voices.Add(new LangGroup { language = _newLanguage, audio = newAudio });
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}