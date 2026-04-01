using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vortex.Steam.SteamConnectionSystem
{
    internal static class Settings
    {
        /// <summary>
        /// Получить ассет. Создаёт при первом обращении, если отсутствует.
        /// </summary>
        public static SteamConnectionSettings GetSettings()
        {
            var res = Resources.LoadAll<SteamConnectionSettings>("");
#if UNITY_EDITOR
            return res.Length > 0 ? res[0] : CreateAsset();
#else
            if (res.Length == 0)
            {
                Debug.LogError("Couldn't load Steam Connection Settings");
                return null;
            }

            return res[0];
#endif
        }
#if UNITY_EDITOR

        /// <summary>
        /// Путь к настройкам системы 
        /// </summary>
        private const string ResourcesRoot = "Assets/Resources/Editor";

        /// <summary>
        /// Явное создание/восстановление ассета
        /// </summary>
        private static SteamConnectionSettings CreateAsset()
        {
            Directory.CreateDirectory(ResourcesRoot);
            var assetName = Path.Combine(ResourcesRoot, "SteamConnectionSettings.asset");
            var settings = ScriptableObject.CreateInstance<SteamConnectionSettings>();
            AssetDatabase.CreateAsset(settings, assetName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SteamSettings] Создан ассет: {ResourcesRoot}");
            return settings;
        }

        /// <summary>
        /// Проверка синхронности записей в настроечном файле и в настройках
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckSteamAppId()
        {
            var settings = GetSettings();

            if (settings.SteamAppId == 0)
                settings.SetAppId(480);
            if (File.Exists("steam_appid.txt"))
                if (uint.TryParse(File.ReadAllText("steam_appid.txt"), out var appId))
                    if (appId == settings.SteamAppId)
                        return;
            settings.OnAppUdChanged();
        }
#endif
    }
}