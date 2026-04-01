#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.DriverManagerSystem.Base;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.DriverManagerSystem.Editor
{
    public static class MenuController
    {
        [MenuItem("Vortex/Configs/Drivers Config")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<DriverConfig>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource[0];
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif