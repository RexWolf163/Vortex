#if UNITY_EDITOR

using UnityEditor;
using Vortex.Sdk.RebindSystem.Presets;
using Vortex.Unity.Extensions;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Sdk.RebindSystem.Editor
{
    /// <summary>
    /// Меню-команда пакета: <c>Tools/Vortex/Configs/Rebind Settings</c> — подсветить <see cref="RebindSettings"/>
    /// в Project window. Сборка пакета собирается только при <c>USING_VORTEX_REBIND</c>, поэтому пункт есть
    /// только при включённом пакете в SdkSettings.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/Rebind Settings")]
        private static void FindConfig()
        {
            var res = AssetDatabaseExt.GetSingletonAsset<RebindSettings>();
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif
