using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.LocalizationSystem;

namespace Vortex.NaniExtensions.LocalizationSystem.Presets
{
    public partial class LocalizationPreset : ScriptableObject
    {
        [InfoBox("Пусть до ресурсов локализации. Можно указать папку для автоматического заполнения.")] [SerializeField]
        private string path;

        [InfoBox("Базовые списки ключей для перевода")] [SerializeField]
        private TextAsset[] files;

        [InfoBox("Языки локализации")] [SerializeField]
        private TextAsset languages;

        [SerializeField, HideInInspector] internal string[] langs;
        [SerializeField, HideInInspector] internal string[] langsKeys;

        [SerializeField, HideInInspector] internal LocalePreset[] localeData;

        [SerializeField, Language] private string defaultLanguage;

        /// <summary>
        /// Возвращает настройку дефолтного языка
        /// </summary>
        /// <returns></returns>
        public string GetDefaultLanguage() => defaultLanguage;
    }
}