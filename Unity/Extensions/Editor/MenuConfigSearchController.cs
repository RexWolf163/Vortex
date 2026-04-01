#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor
{
    /// <summary>
    /// Опорный контроллер для поиска ассетов в иерархии Unity
    /// </summary>
    public static class MenuConfigSearchController
    {
        public static void FindAsset(ScriptableObject res)
        {
            if (res == null)
                return;
            Selection.activeObject = res;
            EditorGUIUtility.PingObject(res);
        }
    }
}
#endif