#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Vortex.Steam.SteamConnectionSystem
{
    /// <summary>
    /// Контроллер добавления define строк по команде
    /// </summary>
    internal static class DefineSymbolManager
    {
        private const string USING_STEAM = "USING_STEAM";

        [InitializeOnLoadMethod]
        internal static void Refresh()
        {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
            var settings = Settings.GetSettings();
            var b = settings.IsEnabled;
#else
            var b = false;
#endif
            ApplyDefineSymbol(b, USING_STEAM);
        }

        /// <summary>
        /// Применяет DEFINE ключ к текущей платформе
        /// </summary>
        /// <param name="shouldEnable"></param>
        /// <param name="defineString"></param>
        internal static void ApplyDefineSymbol(bool shouldEnable, string defineString)
        {
            var targetGroups = new HashSet<BuildTargetGroup>
            {
                EditorUserBuildSettings.selectedBuildTargetGroup,
                //BuildTargetGroup.Standalone
            };

            var modifiedCount = false;

            foreach (var group in targetGroups)
            {
                if (group == BuildTargetGroup.Unknown) continue;

                try
                {
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                    var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

                    var definesSet = new HashSet<string>(
                        currentDefines
                            .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                    );

                    var wasChanged = false;
                    switch (shouldEnable)
                    {
                        case true when !definesSet.Contains(defineString):
                            definesSet.Add(defineString);
                            wasChanged = true;
                            break;
                        case false when definesSet.Contains(defineString):
                            definesSet.Remove(defineString);
                            wasChanged = true;
                            break;
                    }

                    if (wasChanged)
                    {
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget,
                            string.Join(";", definesSet.OrderBy(x => x)));
                        modifiedCount = true;
                        Debug.Log(
                            $"[DefineSymbolManager] {(shouldEnable ? "Добавлен" : "Удалён")} {defineString} для {group}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DefineSymbolManager] ошибка при обработке {group} — {ex.Message}");
                }
            }

            if (!modifiedCount) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif