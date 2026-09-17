using System.Linq;
using UnityEditor;
using Vortex.Unity.EditorTools.HierarchyTools;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.UIBuilder.Shortcuts
{
    /// <summary>
    /// Горячие клавиши <see cref="UIStateSwitcher"/> для выделенных объектов сцены или открытого префаба:
    /// <list type="bullet">
    /// <item>Alt+S (<c>Tools/Vortex/UI/Add UIStateSwitcher</c>) — добавить на сам объект; где он уже есть — пропуск;</item>
    /// <item>Ctrl+Alt+S (<c>Tools/Vortex/UI/Add UIStateSwitcher Layer</c>) — добавить отдельным дочерним слоем
    /// <c>[UIStateSwitcher]</c>: первым в иерархии, в нулевой точке, на обычном Transform.</item>
    /// </list>
    /// Ассеты в Project window не затрагиваются. Отменяется через Undo.
    /// </summary>
    public static class UIStateSwitcherShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add UIStateSwitcher &s";
        private const string LayerMenuPath = "Tools/Vortex/UI/Add UIStateSwitcher Layer %&s";

        [MenuItem(MenuPath)]
        private static void Add() => HierarchyLayers.AddToTargets<UIStateSwitcher>();

        [MenuItem(MenuPath, true)]
        private static bool CanAdd() => HierarchyLayers.Targets().Any();

        [MenuItem(LayerMenuPath)]
        private static void AddLayer() => HierarchyLayers.AddLayerToTargets<UIStateSwitcher>();

        [MenuItem(LayerMenuPath, true)]
        private static bool CanAddLayer() => HierarchyLayers.Targets().Any();
    }
}
