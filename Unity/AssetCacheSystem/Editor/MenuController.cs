#if UNITY_EDITOR

using UnityEditor;
using Vortex.Unity.AssetCacheSystem.Config;
using Vortex.Unity.Extensions;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.AssetCacheSystem.Editor
{
    public static class MenuController
    {
        [MenuItem("Vortex/Configs/AssetCache Settings")]
        private static void FindConfig()
        {
            var res = AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>();
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif