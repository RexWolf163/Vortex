#if UNITY_EDITOR

using System.Linq;
using UnityEditor;
using Vortex.Unity.UI.Shortcuts;

namespace Vortex.Unity.UI.TweenerSystem.Editor
{
    /// <summary>
    /// Горячие клавиши <see cref="TweenerHub"/> для выделенных объектов сцены или открытого префаба:
    /// <list type="bullet">
    /// <item>Alt+T (<c>Tools/Vortex/UI/Add TweenerHub</c>) — добавить на сам объект; где он уже есть — пропуск;</item>
    /// <item>Ctrl+Alt+T (<c>Tools/Vortex/UI/Add TweenerHub Layer</c>) — добавить отдельным дочерним слоем
    /// <c>[TweenerHub]</c>: первым в иерархии, в нулевой точке, на обычном Transform.</item>
    /// </list>
    /// Ассеты в Project window не затрагиваются. Отменяется через Undo.
    /// </summary>
    public static class TweenerHubShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add TweenerHub &t";
        private const string LayerMenuPath = "Tools/Vortex/UI/Add TweenerHub Layer %&t";

        [MenuItem(MenuPath)]
        private static void Add() => ComponentShortcuts.AddToTargets<TweenerHub>();

        [MenuItem(MenuPath, true)]
        private static bool CanAdd() => ComponentShortcuts.Targets().Any();

        [MenuItem(LayerMenuPath)]
        private static void AddLayer() => ComponentShortcuts.AddLayerToTargets<TweenerHub>();

        [MenuItem(LayerMenuPath, true)]
        private static bool CanAddLayer() => ComponentShortcuts.Targets().Any();
    }
}
#endif
