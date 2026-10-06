#if USING_STEAM
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Steam.SteamConnectionSystem.Editor
{
    /// <summary>
    /// Меню-команда быстрого доступа к <see cref="SteamConnectionSettings"/>-ассету
    /// (в <c>Resources/Editor/</c>): <c>Tools/Vortex/Configs/Steam Settings</c>.
    /// Подсвечивает ассет в Project window.
    ///
    /// Пункт меню виден только при включённом <c>USING_STEAM</c> (asmdef имеет
    /// <c>defineConstraints: ["USING_STEAM"]</c>), что совпадает с видимостью
    /// самого ассета.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/Steam Settings")]
        private static void FindConfig()
        {
            var resources = Resources.LoadAll<SteamConnectionSettings>("");
            if (resources == null || resources.Length == 0)
                return;

            MenuConfigSearchController.FindAsset(resources[0]);
        }
    }
}
#endif
#endif
