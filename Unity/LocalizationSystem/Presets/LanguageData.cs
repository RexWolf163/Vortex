using System;
using UnityEngine;

namespace Vortex.Unity.LocalizationSystem.Presets
{
    [Serializable]
    public struct LanguageData
    {
        public LanguageData(string language, string text)
        {
            this.language = language;
            this.text = text.Replace("\\n", "\n").Replace("\\t", "\t");
        }

        [SerializeField] private string language;

        [SerializeField] private string text;
        public string Language => language;
        public string Text => text;

        public override string ToString() => $"<b>{language}</b>:\t\"{text}\"";
    }
}