#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Vortex.Unity.LocalizationSystem.Presets
{
    public partial class LocalizationPreset
    {
        [ShowInInspector, ValueDropdown("GetLocaleData")]
        [InfoBox("@ShowLangsList()")]
        [InfoBox("@ShowLocaleData()")]
        [PropertyOrder(100), HideLabel]
        [TitleGroup("Debug")]
        private string _locale;

        [Button("Load Data")]
        private void RunLoadData() => _ = LoadData();

        internal async UniTask LoadData()
        {
            Debug.Log("[Localization] Loading Started....");

            var tempLangs = new HashSet<string>();
            var index = new Dictionary<string, LocalePreset>();
            foreach (var sheet in sheets)
            {
                using (var www = UnityWebRequest.Get(string.Format(UrlPattern, localeDoc, sheet)))
                {
                    await www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[Localization] Failed to load sheet: {www.error}\n{www.result}");
                        continue;
                    }

                    var data = www.downloadHandler.text;
                    var list = data.Split('\n');
                    list[0] = list[0].TrimEnd('\r');
                    var title = list[0].Split('\t');
                    var temp = new string[title.Length - 1];
                    Array.Copy(title, 1, temp, 0, temp.Length);
                    tempLangs.AddRange(temp);
                    var count = list.Length;
                    for (var i = 1; i < count; i++)
                    {
                        list[i] = list[i].TrimEnd('\r');
                        var tempDataArray = list[i].Split('\t');
                        var key = tempDataArray[0].ToUpper();
                        var tempCount = tempDataArray.Length;
                        if (!index.TryGetValue(key, out var localePreset))
                            localePreset = new LocalePreset(key);
                        var listLangData = localePreset.Texts.ToList();
                        var currentLangs = listLangData.Select(x => x.Language).ToList();
                        for (int j = 1; j < tempCount; j++)
                        {
                            if (currentLangs.Contains(temp[j - 1]))
                            {
                                Debug.LogError($"Language {temp[j - 1]} already exists for {key}");
                                continue;
                            }

                            listLangData.Add(new LanguageData(temp[j - 1], tempDataArray[j]));
                        }

                        localePreset.Texts = listLangData.ToArray();
                        index.Add(key, localePreset);
                    }

                    Debug.Log($"[Localization] Loading sheet {sheet} completed.");
                }
            }

            langs = tempLangs.ToArray();
            localeData = index.Values.ToArray();

            foreach (var lang in langs)
                if (!Enum.TryParse(typeof(SystemLanguage), lang, true, out _))
                    Debug.LogError($"[Localization] Language {lang} is not supported.");

            EditorUtility.SetDirty(this);
            Debug.Log("[Localization] Loading Complete");
        }

        private ValueDropdownList<string> GetLocaleData()
        {
            var list = new ValueDropdownList<string>();
            foreach (var preset in localeData)
                list.Add(preset.Key);

            return list;
        }

        private string ShowLocaleData()
        {
            if (_locale.IsNullOrWhitespace())
                return "Выберите ключ для просмотра";
            foreach (var data in localeData)
            {
                if (data.Key == _locale)
                    return $"{string.Join("\n", data.Texts)}";
            }

            return "Not Found data for this key";
        }

        private string ShowLangsList() => $"<b>Зафиксированные языки:</b>\n{string.Join("\n", langs)}";

        [Button, PropertyOrder(110)]
        private void CheckSystemLanguage() => Debug.Log("Current language: " + Application.systemLanguage);
    }
}
#endif