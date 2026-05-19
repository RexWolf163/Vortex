#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Sdk.SdkSettingsSystem.Editor
{
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/SDK Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<SdkSettings>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource[0];
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif