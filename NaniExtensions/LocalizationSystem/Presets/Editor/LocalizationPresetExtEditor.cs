#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.LocalizationSystem.Presets;
using UniTask = Cysharp.Threading.Tasks.UniTask;

namespace Vortex.NaniExtensions.LocalizationSystem.Presets
{
    public partial class LocalizationPreset
    {
        [ShowInInspector, OnValueChanged("GetPath"), PropertyOrder(-100)]
        private DefaultAsset folder;

        [ShowInInspector, ValueDropdown("GetLocaleData")]
        [InfoBox("@ShowLocaleData()")]
        [PropertyOrder(100), HideLabel]
        [TitleGroup("Debug")]
        private string _locale;

        /// <summary>
        /// Исходный язык локализации
        /// </summary>
        private string _fallbackLanguage;

        /// <summary>
        /// Кеш названий файлов ассетов
        /// </summary>
        private string[] _filesNames;

        /// <summary>
        /// защита от раннего перезапуска
        /// </summary>
        private bool _run;

        /// <summary>
        /// Предварительный список языков из файла
        /// двусимвольный ключ языка => Полное название
        /// </summary>
        private readonly Dictionary<string, string> _tempLangs = new();

        /// <summary>
        /// Получить дефолтный языке из конфига Нани
        /// </summary>
        /// <returns></returns>
        private string GetFallbackLang()
        {
            if (!_fallbackLanguage.IsNullOrWhitespace())
                return _fallbackLanguage;
            ReadConfig();
            return _fallbackLanguage;
        }

        /// <summary>
        /// Читаем конфиг нани
        /// </summary>
        private void ReadConfig()
        {
            var assets = AssetDatabase.FindAssets("t:LocalizationConfiguration");
            if (assets.Length == 0)
                return;
            var path = AssetDatabase.GUIDToAssetPath(assets[0]);
            var config = AssetDatabase.LoadAssetAtPath<LocalizationConfiguration>(path);
            _fallbackLanguage = config.DefaultLocale;
        }

        internal async UniTask LoadData()
        {
            if (_run)
                return;
            try
            {
                _run = true;
                Debug.Log("[Localization] Loading Started....");

                var index = new Dictionary<string, LocalePreset>();

                var lines = languages.text.Split('\n');

                _tempLangs.Clear();

                foreach (var line in lines)
                {
                    if (line.IsNullOrWhitespace())
                        continue;
                    var temp = line.Split(":");
                    var key = temp[0].Trim();
                    if (key.IsNullOrWhitespace())
                        continue;
                    _tempLangs.Add(key, temp[1].Trim());
                }

                ReadConfig();
                var defaultLocale = GetFallbackLang();
                foreach (var textAsset in files)
                {
                    lines = textAsset.text.Split('\n');
                    foreach (var line in lines)
                    {
                        var ar = line.Split(':');
                        if (ar.Length < 2)
                            continue; //фильтрация мусора
                        var key = ar[0].Trim();
                        if (key.IsNullOrWhitespace())
                            continue;
                        var value = string.Join(':', ar[1..]).Trim();
                        index[key] = new LocalePreset(key);
                        index[key].SetLangData(new LanguageData(defaultLocale, value));
                    }
                }

                var checkLangsList = new HashSet<string> { defaultLocale };
                _filesNames = files.Select(f => f.name).ToArray();

                await ParseFolder(path, index, checkLangsList);

                langsKeys = checkLangsList.ToArray();
                langs = langsKeys.Select(lang => _tempLangs[lang]).ToArray();
                localeData = index.Values.ToArray();

                EditorUtility.SetDirty(this);
            }
            finally
            {
                _run = false;
                Debug.Log("[Localization] Loading Complete");
            }
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

        [Button, PropertyOrder(110)]
        private void CheckSystemLanguage() => Debug.Log("Current language: " + Application.systemLanguage);

        private void GetPath()
        {
            if (folder == null)
                return;
            path = "";
            var fPath = AssetDatabase.GetAssetPath(folder);
            if (AssetDatabase.IsValidFolder(fPath))
                path = fPath.Remove("Assets").TrimStart('\\', '/');

            folder = null;
            EditorUtility.SetDirty(this);
        }

        private async UniTask ParseFolder(string path, Dictionary<string, LocalePreset> index, HashSet<string> langs)
        {
            var combPath = Path.Combine(Application.dataPath, path);
            var dirs = Directory.GetDirectories(combPath);
            foreach (var d in dirs)
            {
                var dir = d.Substring(combPath.Length + 1);
                var subPath = Path.Combine(path, dir);
                if (!_tempLangs.ContainsKey(dir))
                    await ParseFolder(subPath, index, langs);
                else
                    await ParseLangData(subPath, index, langs);

                await UniTask.Yield();
            }
        }

        private async UniTask ParseLangData(string path, Dictionary<string, LocalePreset> index, HashSet<string> langs)
        {
            var langName = Path.GetFileName(path);
            if (langName.IsNullOrWhitespace())
            {
                Debug.LogError($"[Localization] Language data at {path} is broken.");
                return;
            }

            if (!langs.Contains(langName))
                langs.Add(langName);

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { Path.Combine("Assets", path) });
            var assets = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToList();

            foreach (var asset in assets)
            {
                if (!_filesNames.Contains(asset.name))
                    continue;
                var file = files.First(f => f.name == asset.name);
                var lines = file.text.Split('\n');

                var keys = new HashSet<string>();

                //Первичное заполнение списка ключей
                foreach (var line in lines)
                {
                    var ar = line.Split(':');
                    if (ar.Length < 2)
                        continue; //фильтрация мусора
                    var key = ar[0].Trim();
                    if (key.IsNullOrWhitespace())
                        continue;
                    var value = string.Join(':', ar[1..]).Trim();
                    index[key].SetLangData(new LanguageData(langName, value));
                    keys.Add(key);
                }

                await UniTask.Yield();

                lines = asset.text.Split('\n');
                //окончательное заполнение тем что найдено
                foreach (var line in lines)
                {
                    var ar = line.Split(':');
                    if (ar.Length < 2)
                        continue; //фильтрация мусора
                    var key = ar[0].Trim();

                    if (key.IsNullOrWhitespace())
                        continue;

                    if (!index.ContainsKey(key))
                    {
                        Debug.LogError($"[Localization] Wrong key #{key}.");
                        continue;
                    }

                    var value = string.Join(':', ar[1..]).Trim();
                    if (value.IsNullOrWhitespace())
                    {
                        Debug.LogError($"[Localization] Empty locale for #{key}.");
                        continue;
                    }

                    index[key].SetLangData(new LanguageData(langName, value));
                    keys.Remove(key);
                }

                foreach (var key in keys)
                    Debug.LogError($"[Localization] Key: {key} not found in {langName} locale.");
            }
        }
    }
}

#endif