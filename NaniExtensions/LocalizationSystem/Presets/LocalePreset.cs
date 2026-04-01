using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.LocalizationSystem.Presets;

namespace Vortex.NaniExtensions.LocalizationSystem.Presets
{
    /// <summary>
    /// Preset для локализуемого фрагмента текста
    /// </summary>
    [Serializable]
    public class LocalePreset
    {
        public LocalePreset(string key)
        {
            this.key = key;
            texts = new List<LanguageData>();
        }

        /// <summary>
        /// Ключ фрагмента текста
        /// </summary>
        [SerializeField] private string key;

        /// <summary>
        /// Переводы на разные языки
        /// </summary>
        [SerializeField] private List<LanguageData> texts;

        public string Key => key;

        public IReadOnlyList<LanguageData> Texts => texts;

        internal void SetLangData(LanguageData value)
        {
            texts ??= new List<LanguageData>();
            for (var i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                if (text.Language != value.Language) continue;
                texts[i] = value;
                return;
            }

            texts.Add(value);
        }
    }
}