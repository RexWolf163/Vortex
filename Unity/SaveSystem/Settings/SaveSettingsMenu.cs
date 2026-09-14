#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.SaveSystem.Presets
{
    /// <summary>Меню-команда: <c>Tools/Vortex/Configs/Save Settings</c> — подсветить ассет настроек сохранений.</summary>
    public static class SaveSettingsMenu
    {
        [MenuItem("Tools/Vortex/Configs/Save Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<SaveSettings>("");
            if (resource == null || resource.Length == 0)
                return;
            MenuConfigSearchController.FindAsset(resource[0]);
        }
    }
}
#endif
