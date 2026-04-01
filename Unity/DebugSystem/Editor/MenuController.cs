#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.DebugSystem.Editor
{
    public static class MenuController
    {
        [MenuItem("Vortex/Configs/Debug Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<StartSettings>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource[0];
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif