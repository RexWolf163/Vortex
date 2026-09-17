#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.HierarchyTools
{
    /// <summary>
    /// Общая логика editor-команд «добавить компонент» и «добавить слой» в иерархию: прямо на выделенные объекты
    /// или отдельным дочерним слоем. Работает с объектами сцены и открытого префаба; ассеты в Project window не
    /// затрагиваются. Пользуются горячие клавиши UIBuilder и Sync у <c>StateView</c>.
    /// </summary>
    public static class HierarchyLayers
    {
        /// <summary>Выделенные объекты сцены или открытого префаба.</summary>
        public static GameObject[] Targets() =>
            Selection.gameObjects.Where(go => !EditorUtility.IsPersistent(go)).ToArray();

        /// <summary>
        /// Добавить компонент на выделенные объекты. Где он уже есть — пропуск: повторное нажатие не плодит дубли.
        /// </summary>
        public static void AddToTargets<T>() where T : Component
        {
            foreach (var target in Targets())
                if (target.GetComponent<T>() == null)
                    Undo.AddComponent<T>(target);
        }

        /// <summary>
        /// Добавить компонент отдельным слоем <c>[{тип}]</c> в каждый выделенный объект (<see cref="CreateLayer{T}"/>).
        /// </summary>
        public static void AddLayerToTargets<T>() where T : Component
        {
            var name = $"[{typeof(T).Name}]";
            AddLayersToTargets(parent => CreateLayer<T>(parent, name).gameObject);
        }

        /// <summary>
        /// Создать слой в каждом выделенном объекте через <paramref name="create"/>. Повторный вызов создаёт ещё
        /// один слой. Новые слои выделяются; отмена — одним шагом Undo.
        /// </summary>
        public static void AddLayersToTargets(Func<GameObject, GameObject> create)
        {
            var created = new List<GameObject>();

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            foreach (var parent in Targets())
                created.Add(create(parent));

            Undo.CollapseUndoOperations(group);

            if (created.Count > 0)
                Selection.objects = created.ToArray<Object>();
        }

        /// <summary>
        /// Слой с компонентом: дочерний объект <paramref name="parent"/> с именем <paramref name="name"/> — первым
        /// в иерархии родителя, в нулевой точке (позиция и поворот 0, масштаб 1), на обычном Transform (RectTransform,
        /// если его накинет Unity, снимается). Создание регистрируется в Undo; группировку ведёт вызывающий.
        /// </summary>
        public static T CreateLayer<T>(GameObject parent, string name) where T : Component
        {
            var layer = new GameObject(name);
            // Родитель и его слой (Layer) — как у штатного «Create Empty Child»
            GameObjectUtility.SetParentAndAlign(layer, parent);

            if (layer.TryGetComponent<RectTransform>(out var rect))
                Object.DestroyImmediate(rect);

            var transform = layer.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            transform.SetAsFirstSibling();

            var component = layer.AddComponent<T>();

            // Регистрация после сборки: отмена удаляет слой целиком вместе со всеми правками
            Undo.RegisterCreatedObjectUndo(layer, $"Add {name} layer");
            return component;
        }

        /// <summary>
        /// UI-слой: дочерний объект <paramref name="parent"/> с именем <paramref name="name"/> — первым в иерархии
        /// родителя, RectTransform растянут по родителю (якоря 0–1, отступы 0, pivot по центру, поворот 0,
        /// масштаб 1). <paramref name="configure"/> добавляет компоненты до регистрации в Undo. Группировку Undo
        /// ведёт вызывающий.
        /// </summary>
        public static GameObject CreateStretchedLayer(GameObject parent, string name, Action<GameObject> configure)
        {
            var layer = new GameObject(name, typeof(RectTransform));
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

            configure(layer);

            Undo.RegisterCreatedObjectUndo(layer, $"Add {name} layer");
            return layer;
        }
    }
}
#endif
