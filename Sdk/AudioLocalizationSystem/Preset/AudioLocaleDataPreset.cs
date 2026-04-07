using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
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
#endif
    }
}