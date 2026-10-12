#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.UI.VirtualCursorSystem.InputDrivers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Editor
{
    /// <summary>
    /// Меню-команды быстрого доступа к ассетам пакета:
    /// <c>Tools/Vortex/Configs/Input Driver Set</c> — подсветить
    /// <see cref="InputDriverSet"/> (конфиг драйверов ввода курсора).
    /// Ассет лежит в <c>Resources/Settings/</c> и создаётся <c>CoreAssetsController</c>-ом при первом запуске.
    /// </summary>
    public static class MenuController
    {
        /// <summary>Найти <see cref="InputDriverSet"/> в Resources и подсветить его в Project. Нет ассета — ничего не делает.</summary>
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