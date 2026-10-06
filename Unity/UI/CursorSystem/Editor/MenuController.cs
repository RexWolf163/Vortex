#if UNITY_EDITOR
// Легаси-курсор отключён, когда стоит USING_VORTEX_CURSOR (см. CursorController.Init
// под тем же гвардом). Пункт меню убираем целиком, чтобы в Tools/Vortex/Configs
// не было двух похожих записей (есть Virtual Cursor Skin Settings из нового пакета).
#if !USING_VORTEX_CURSOR

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
#endif
