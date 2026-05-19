#if UNITY_EDITOR

using UnityEditor;
using Vortex.Unity.AssetCacheSystem.Config;
using Vortex.Unity.Extensions;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.AssetCacheSystem.Editor
{
    /// <summary>
    /// Меню-команда быстрого доступа к единственному <see cref="AssetCacheSettings"/>-ассету
    /// в проекте: <c>Vortex/Configs/AssetCache Settings</c>. Подсвечивает ассет в Project window.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/AssetCache Settings")]
        private static void FindConfig()
        {
            var res = AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>();
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif