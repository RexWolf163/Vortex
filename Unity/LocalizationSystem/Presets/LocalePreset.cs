using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Vortex.Unity.LocalizationSystem.Presets
{
    [Serializable]
    public struct LocalePreset
    {
        public LocalePreset(string key)
        {
            this.key = key;
            texts = new LanguageData[] { };
        }

        [FormerlySerializedAs("name")] [SerializeField] private string key;
        [SerializeField] private LanguageData[] texts;

        public string Key => key;

        public LanguageData[] Texts
        {
            get => texts;
            internal set => texts = value;
        }
    }
}