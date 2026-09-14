#if UNITY_EDITOR

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem.Editor
{
    /// <summary>
    /// Горячая клавиша Alt+H (<c>Tools/Vortex/UI/Add TweenerHub</c>): добавить <see cref="TweenerHub"/> на
    /// выделенные объекты сцены или открытого префаба. Объекты, где он уже есть, пропускаются — повторное
    /// нажатие не плодит дубли. Ассеты в Project window не затрагиваются. Отменяется через Undo.
    /// </summary>
    public static class TweenerHubShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add TweenerHub &h";

        [MenuItem(MenuPath)]
        private static void Add()
        {
            foreach (var target in Targets())
                if (target.GetComponent<TweenerHub>() == null)
                    Undo.AddComponent<TweenerHub>(target);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAdd() => Targets().Any();

        private static GameObject[] Targets() =>
            Selection.gameObjects.Where(go => !EditorUtility.IsPersistent(go)).ToArray();
    }
}
#endif
