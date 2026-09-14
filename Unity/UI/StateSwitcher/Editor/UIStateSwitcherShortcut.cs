#if UNITY_EDITOR

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.StateSwitcher.Editor
{
    /// <summary>
    /// Горячая клавиша Alt+S (<c>Tools/Vortex/UI/Add UIStateSwitcher</c>): добавить <see cref="UIStateSwitcher"/>
    /// на выделенные объекты сцены или открытого префаба. Объекты, где он уже есть, пропускаются — повторное
    /// нажатие не плодит дубли. Ассеты в Project window не затрагиваются. Отменяется через Undo.
    /// </summary>
    public static class UIStateSwitcherShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add UIStateSwitcher &s";

        [MenuItem(MenuPath)]
        private static void Add()
        {
            foreach (var target in Targets())
                if (target.GetComponent<UIStateSwitcher>() == null)
                    Undo.AddComponent<UIStateSwitcher>(target);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAdd() => Targets().Any();

        private static GameObject[] Targets() =>
            Selection.gameObjects.Where(go => !EditorUtility.IsPersistent(go)).ToArray();
    }
}
#endif
