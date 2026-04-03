#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.FileSystem.Bus;
using LocalizationPreset = Vortex.NaniExtensions.LocalizationSystem.Presets.LocalizationPreset;

namespace Vortex.NaniExtensions.LocalizationSystem
{
    public partial class LocalizationDriver
    {
        private static bool _isSet;

        [InitializeOnLoadMethod]
        private static void EditorRegister()
        {
            _isSet = false;
            if (!Localization.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            File.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LocalizationPreset>(),
                    $"Assets/Resources/{Path}/LocalizationDataNaninovell.asset");
                Debug.Log("Create new settings preset LocalizationData for Naninovell system");
            }


            _isSet = true;
            Instance.LoadData();
            RefreshIndex();
        }

        [MenuItem("Vortex/Localization/(Nani) Load data", false, 1)]
        private static async void LoadLocalizationData()
        {
            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                Debug.LogError("[Localization] Localization Preset not found]");
                return;
            }

            _resource = resources[0];
            await _resource.LoadData();
            RefreshIndex();
        }

        [MenuItem("Vortex/Localization/Set Preset default Locale", false, 1)]
        public static void SetDefaultLocale()
        {
            if (Instance == null)
                return;
            var currentLang = Instance.GetPresetDefaultLanguage();
            Localization.SetCurrentLanguage(currentLang);
        }

        private void LoadData()
        {
            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                Debug.LogError("Localization Data asset not found");
                return;
            }

            _resource = resources[0];
            RefreshIndex();
        }

        private static void RefreshIndex()
        {
            if (_localeData == null)
                return;
            _localeData.Clear();
            var currentLanguage = Localization.GetCurrentLanguage();
            foreach (var data in _resource.localeData)
            {
                if (data.Texts.Count == 0)
                {
                    Debug.LogError($"[LocalizationDriver] Localization data is broken");
                    continue;
                }

                var n = -1;
                for (var i = 0; i < data.Texts.Count; i++)
                {
                    var dataText = data.Texts[i];
                    if (dataText.Language != currentLanguage) continue;
                    n = i;
                    break;
                }

                if (n == -1)
                {
                    _localeData.AddNew(data.Key, data.Texts[0].Text);
                    continue;
                }

                var translateData = data.Texts[n];
                _localeData.AddNew(data.Key, translateData.Text);
            }
        }

        [MenuItem("Vortex/Localization/(Nani) Load data", true)]
        [MenuItem("Vortex/Localization/Set Preset default Locale", true)]
        private static bool CheckDriver() => _isSet;
    }
}
#endif