using System.Linq;
using UnityEditor;
using UnityEngine.UI;
using Vortex.Unity.EditorTools.HierarchyTools;

namespace Vortex.Unity.UI.UIBuilder.Shortcuts
{
    /// <summary>
    /// Alt+I (<c>Tools/Vortex/UI/Add BackgroundLayer</c>) — в каждый выделенный объект сцены или открытого
    /// префаба добавить дочерний слой <c>Background</c> с <see cref="Image"/> (цвет — из настроек UIBuilder,
    /// Maskable выключен, Raycast Target включён): первым в иерархии (рисуется под остальными детьми),
    /// RectTransform растянут по родителю. Повторное нажатие создаёт ещё один слой.
    /// Новые слои выделяются; отмена — одним шагом Undo.
    /// </summary>
    public static class BackgroundLayerShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add BackgroundLayer &i";
        private const string LayerName = "Background";

        [MenuItem(MenuPath)]
        private static void AddLayer() =>
            HierarchyLayers.AddLayersToTargets(parent => HierarchyLayers.CreateStretchedLayer(parent, LayerName,
                layer =>
                {
                    var image = layer.AddComponent<Image>();
                    image.color = UIBuilderSettings.instance.BackgroundColor;
                    // Фон не участвует в маскировании; raycastTarget остаётся по умолчанию (true)
                    image.maskable = false;
                }));

        [MenuItem(MenuPath, true)]
        private static bool CanAddLayer() => HierarchyLayers.Targets().Any();
    }
}
