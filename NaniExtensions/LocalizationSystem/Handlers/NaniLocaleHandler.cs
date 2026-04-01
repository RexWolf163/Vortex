using System;
using System.Collections.Generic;
using System.Linq;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.NaniExtensions.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.NaniExtensions.LocalizationSystem.Handlers
{
    public class NaniLocaleHandler : MonoBehaviour
    {
        private readonly List<string> _availableLocales = new();
        private readonly List<string> _availableLocalesLabels = new();

        [SerializeField, ValueDropdown("NaniLanguages")]
        private string[] whiteList;

        [SerializeField] private DropDownComponent dropdown;

        [SerializeField] private LocaleChannels mode = LocaleChannels.UI;

        private void Awake()
        {
            CheckLocaleLists();
            Refresh();
        }

        private void OnEnable()
        {
            Localization.OnLocalizationChanged += Refresh;
        }

        private void OnDisable()
        {
            Localization.OnLocalizationChanged -= Refresh;
        }

        private void Refresh()
        {
            var lang = String.Empty;
            switch (mode)
            {
                case LocaleChannels.UI:
                    lang = Localization.GetCurrentLanguage();
                    break;
                case LocaleChannels.Dialogue:
                    lang = Localization.GetCurrentDialogueLanguage();
                    break;
                case LocaleChannels.Voice:
                    lang = Localization.GetCurrentVoiceLanguage();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var currentLocale = _availableLocales.IndexOf(lang);
            dropdown.SetList(_availableLocalesLabels.ToArray(), SetLocale, currentLocale);
        }

        public void SetLocale(int value)
        {
            var selectedLocale = _availableLocales[value];
            switch (mode)
            {
                case LocaleChannels.UI:
                    Localization.SetCurrentLanguage(selectedLocale);
                    break;
                case LocaleChannels.Dialogue:
                    Localization.SetCurrentDialogueLanguage(selectedLocale);
                    break;
                case LocaleChannels.Voice:
                    Localization.SetCurrentVoiceLanguage(selectedLocale);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void GetAvailableLocales()
        {
            var ar = NaniWrapper.L10N.AvailableLocales.ToArray();
            _availableLocales.Clear();
            _availableLocalesLabels.Clear();
            foreach (var lang in ar)
            {
                if (!whiteList.Contains(lang))
                    continue;
                _availableLocales.Add(lang);
                _availableLocalesLabels.Add(lang.ToUpper().Translate());
            }
        }

        private void CheckLocaleLists()
        {
            if (_availableLocales.Count == 0 || _availableLocalesLabels.Count == 0)
                GetAvailableLocales();
        }

#if UNITY_EDITOR
        private List<string> NaniLanguages()
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:LocalizationConfiguration");

            if (guids.Length == 0)
                return result;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<LocalizationConfiguration>(path);
                if (config != null && config.Languages != null)
                    result.AddRange(config.Languages.Select(lang => lang.Tag));
            }

            result.Sort();
            return result;
        }

#endif
    }
}