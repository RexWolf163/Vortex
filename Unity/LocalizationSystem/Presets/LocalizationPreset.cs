using UnityEngine;

namespace Vortex.Unity.LocalizationSystem.Presets
{
    public partial class LocalizationPreset : ScriptableObject
    {
        private const string UrlPattern = "https://docs.google.com/spreadsheets/d/{0}/export?format=tsv&gid={1}";

        [SerializeField] private string localeDoc;
        [SerializeField] private string[] sheets;

        [SerializeField, HideInInspector] internal string[] langs;

        [SerializeField, HideInInspector] internal LocalePreset[] localeData;
    }
}