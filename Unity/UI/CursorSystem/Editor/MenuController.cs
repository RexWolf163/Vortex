#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.UI.CursorSystem.Editor
{
    /// <summary>
    /// Меню-команда быстрого доступа к <see cref="CursorSettings"/>-ассету (в Resources):
    /// <c>Tools/Vortex/Configs/Cursor Settings</c>. Подсвечивает ассет в Project window.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/Cursor Settings")]
        private static void FindConfig()
        {
            var resources = Resources.LoadAll<CursorSettings>("");
            if (resources == null || resources.Length == 0)
                return;

            MenuConfigSearchController.FindAsset(resources[0]);
        }
    }
}
#endif
