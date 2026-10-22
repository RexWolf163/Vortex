#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Sdk.Core.Presets
{
    /// <summary>Меню-команда: <c>Tools/Vortex/Configs/Game Settings</c> — подсветить ассет настроек игры.</summary>
    public static class GameSettingsMenu
    {
        [MenuItem("Tools/Vortex/Configs/Game Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<GameSettings>("");
            if (resource == null || resource.Length == 0)
                return;
            MenuConfigSearchController.FindAsset(resource[0]);
        }
    }
}
#endif
