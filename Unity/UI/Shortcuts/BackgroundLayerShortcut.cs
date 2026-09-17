#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.Shortcuts
{
    /// <summary>
    /// Alt+I (<c>Tools/Vortex/UI/Add BackgroundLayer</c>) — в каждый выделенный объект сцены или открытого
    /// префаба добавить дочерний слой <c>Background</c> с <see cref="Image"/> (Maskable выключен, Raycast Target
    /// включён): первым в иерархии (рисуется под
    /// остальными детьми), RectTransform растянут по родителю. Повторное нажатие создаёт ещё один слой.
    /// Новые слои выделяются; отмена — одним шагом Undo.
    /// </summary>
    public static class BackgroundLayerShortcut
    {
        private const string MenuPath = "Tools/Vortex/UI/Add Background Layer &i";
        private const string LayerName = "Background";

        [MenuItem(MenuPath)]
        private static void AddLayer()
        {
            var created = new List<GameObject>();

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            foreach (var parent in ComponentShortcuts.Targets())
                created.Add(CreateLayer(parent));

            Undo.CollapseUndoOperations(group);

            if (created.Count > 0)
                Selection.objects = created.ToArray<Object>();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAddLayer() => ComponentShortcuts.Targets().Any();

        private static GameObject CreateLayer(GameObject parent)
        {
            var layer = new GameObject(LayerName, typeof(RectTransform));
            // Родитель и его слой (Layer) — как у штатного «Create Empty Child»
            GameObjectUtility.SetParentAndAlign(layer, parent);

            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            // Фон не участвует в маскировании; raycastTarget остаётся по умолчанию (true)
            layer.AddComponent<Image>().maskable = false;

            // Регистрация после сборки: отмена удаляет слой целиком вместе со всеми правками
            Undo.RegisterCreatedObjectUndo(layer, $"Add {LayerName} layer");
            return layer;
        }
    }
}
#endif
