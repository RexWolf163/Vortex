#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Core.LocalizationSystem.Bus
{
    public partial class Localization
    {
        private const string FindConfigMenu = "Tools/Vortex/Localization/Find Config";

        /// <summary>
        /// Подсветить ассет данных локализации активного драйвера. У каждого драйвера (Unity, Nani) свой класс
        /// <c>LocalizationPreset</c> в своей сборке, поэтому ассет выбирается по сборке подключённого драйвера.
        /// </summary>
        // Приоритеты подменю: 1 — загрузка данных (драйверы), 20 — локали, 40 — конфиг (разрыв > 10 даёт разделитель).
        // Все ниже 1000 (дефолт соседних подменю Tools/Vortex): позицию подменю Unity берёт из приоритетов пунктов
        [MenuItem(FindConfigMenu, false, 40)]
        private static void FindConfig()
        {
            var driverAssembly = Driver.GetType().Assembly;
            var preset = AssetDatabase.FindAssets("t:LocalizationPreset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null && asset.GetType().Assembly == driverAssembly);
            if (preset == null)
            {
                Debug.LogError($"[Localization] LocalizationPreset драйвера {Driver.GetType().Name} не найден.");
                return;
            }

            // Файл собирается в core-сборку (asmref): MenuConfigSearchController из Unity-слоя недоступен
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        [MenuItem(FindConfigMenu, true)]
        private static bool FindConfigValidate() => Driver != null;

        [MenuItem("Tools/Vortex/Localization/Set Default Locale", false, 20)]
        public static void SetDefaultLocale()
        {
            SetCurrentLanguage(Driver.GetDefaultLanguage());
        }

        [MenuItem("Tools/Vortex/Localization/Set Next Locale", false, 21)]
        public static void SetNextLocale()
        {
            var langs = Driver.GetLanguages();
            var currentLang = GetCurrentLanguage();
            var index = Array.IndexOf(langs, currentLang) + 1;
            if (index < 0 || index >= langs.Length)
                index = 0;

            SetCurrentLanguage(langs[index]);
        }

        public static List<string> GetLanguages()
        {
            var res = new List<string>();
            var langs = Driver.GetLanguages();
            foreach (var lang in langs)
                res.Add(lang);
            return res;
        }

        public static List<string> GetLocalizationKeys()
        {
            var res = new List<string>();
            var texts = Index.Keys.ToArray();
            foreach (var value in texts)
                res.Add(value);
            return res;
        }
    }
}
#endif