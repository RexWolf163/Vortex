#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.UI.VirtualCursorSystem.InputDrivers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Editor
{
    /// <summary>
    /// Меню-команды быстрого доступа к ассетам пакета:
    /// <c>Tools/Vortex/Configs/Virtual Cursor Skin Settings</c> — подсветить
    /// <see cref="CursorSkinSettings"/> (темы курсора);
    /// <c>Tools/Vortex/Configs/Input Driver Set</c> — подсветить
    /// <see cref="InputDriverSet"/> (конфиг драйверов ввода курсора).
    /// Оба ассета лежат в <c>Resources/Settings/</c>, оба создаются
    /// <c>CoreAssetsController</c>-ом при первом запуске.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Configs/Virtual Cursor Skin Settings")]
        private static void FindSkinSettings()
        {
            var resources = Resources.LoadAll<CursorSkinSettings>("");
            if (resources == null || resources.Length == 0)
                return;

            MenuConfigSearchController.FindAsset(resources[0]);
        }

        [MenuItem("Tools/Vortex/Configs/Input Driver Set")]
        private static void FindInputDriverSet()
        {
            var resources = Resources.LoadAll<InputDriverSet>("");
            if (resources == null || resources.Length == 0)
                return;

            MenuConfigSearchController.FindAsset(resources[0]);
        }
    }
}
#endif
