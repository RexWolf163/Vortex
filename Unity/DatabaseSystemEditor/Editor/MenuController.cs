#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.DatabaseSystem.DbSettings;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.DatabaseSystemEditor.Editor
{
    public static class MenuController
    {
        [MenuItem("Vortex/Configs/Database Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<DatabaseSettings>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource[0];
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif