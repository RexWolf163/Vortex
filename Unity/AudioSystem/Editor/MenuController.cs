#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.AudioSystem.Presets;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.DebugSystem.Editor
{
    public static class MenuController
    {
        [MenuItem("Vortex/Configs/Audio Channels Settings")]
        private static void FindConfig()
        {
            var resource = Resources.LoadAll<AudioChannelsConfig>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource[0];
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif