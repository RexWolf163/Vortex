#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.Shortcuts
{
    /// <summary>
    /// Общая логика горячих клавиш пакета UI «добавить компонент»: прямо на выделенные объекты или отдельным
    /// слоем. Работает с объектами сцены и открытого префаба; ассеты в Project window не затрагиваются.
    /// </summary>
    internal static class ComponentShortcuts
    {
        /// <summary>Выделенные объекты сцены или открытого префаба.</summary>
        internal static GameObject[] Targets() =>
            Selection.gameObjects.Where(go => !EditorUtility.IsPersistent(go)).ToArray();

        /// <summary>
        /// Добавить компонент на выделенные объекты. Где он уже есть — пропуск: повторное нажатие не плодит дубли.
        /// </summary>
        internal static void AddToTargets<T>() where T : Component
        {
            foreach (var target in Targets())
                if (target.GetComponent<T>() == null)
                    Undo.AddComponent<T>(target);
        }

        /// <summary>
        /// Добавить компонент отдельным слоем <c>[{тип}]</c> в каждый выделенный объект (<see cref="CreateLayer{T}"/>).
        /// Повторное нажатие создаёт ещё один слой. Новые слои выделяются; отмена — одним шагом Undo.
        /// </summary>
        internal static void AddLayerToTargets<T>() where T : Component
        {
            var name = $"[{typeof(T).Name}]";
            var created = new List<GameObject>();

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            foreach (var parent in Targets())
                created.Add(CreateLayer<T>(parent, name).gameObject);

            Undo.CollapseUndoOperations(group);

            if (created.Count > 0)
                Selection.objects = created.ToArray<Object>();
        }

        /// <summary>
        /// Слой с компонентом: дочерний объект <paramref name="parent"/> с именем <paramref name="name"/> — первым
        /// в иерархии родителя, в нулевой точке (позиция и поворот 0, масштаб 1), на обычном Transform (RectTransform,
        /// если его накинет Unity, снимается). Создание регистрируется в Undo; группировку ведёт вызывающий.
        /// </summary>
        internal static T CreateLayer<T>(GameObject parent, string name) where T : Component
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
    }
}
#endif
