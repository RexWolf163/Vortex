using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.Camera.Model;
using Vortex.Unity.Camera.View;

namespace Vortex.Unity.Camera.Controllers
{
    /// <summary>
    /// Контроллер управления фокусом.
    /// Можно задавать несколько трансформов как группу. Камера будет позиционироваться по центру этой группы.
    /// При нескольких группах для фокуса, камера центрируется на последней.
    /// Если последняя группа удалена, камера вернет фокус на предыдущую.
    /// </summary>
    public static class CameraFocusController
    {
        /// <summary>
        /// Добавить объект в текущий фокус камеры
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="target"></param>
        public static void AddInFocus(this CameraDataStorage storage, Transform target)
        {
            if (storage.Data.focusedObjects.Count == 0)
                storage.Data.focusedObjects.Add(new CameraFocusTarget());
            var group = storage.Data.focusedObjects[^1];
            group.focusTargets.Add(target);

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Добавить объекты в текущий фокус камеры
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="objects"></param>
        public static void AddInFocus(this CameraDataStorage storage, ICollection<Transform> objects)
        {
            if (storage.Data.focusedObjects.Count == 0)
                storage.Data.focusedObjects.Add(new CameraFocusTarget());
            var group = storage.Data.focusedObjects[^1];
            group.focusTargets.AddRange(objects);

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Поставить новые объекты как единственный фокус камеры
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="objects"></param>
        public static void SetNewFocusGroup(this CameraDataStorage storage, ICollection<Transform> objects)
        {
            var group = new CameraFocusTarget();
            group.focusTargets.AddRange(objects);

            storage.Data.focusedObjects.Clear();
            storage.Data.focusedObjects.Add(group);

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Поставить новый объект как единственный фокус камеры
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="target"></param>
        public static void SetNewFocusGroup(this CameraDataStorage storage, Transform target)
        {
            var group = new CameraFocusTarget();
            group.focusTargets.Add(target);

            storage.Data.focusedObjects.Clear();
            storage.Data.focusedObjects.Add(group);

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Удалить все группы фокуса
        /// </summary>
        public static void ResetFocus(this CameraDataStorage storage)
        {
            storage.Data.focusedObjects.Clear();

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Удалить текущую группу фокуса
        /// </summary>
        /// <param name="storage"></param>
        public static void RemoveLastFocusGroup(this CameraDataStorage storage)
        {
            var last = storage.Data.focusedObjects.Count - 1;
            if (last < 0)
                return;
            storage.Data.focusedObjects.RemoveAt(last);

            storage.Data.CallOnUpdate();
        }

        /// <summary>
        /// Удалить из всех групп фокуса указанный объект
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="target"></param>
        public static void RemoveTargetFromFocus(this CameraDataStorage storage, Transform target)
        {
            foreach (var group in storage.Data.focusedObjects)
                group.focusTargets.Remove(target);

            storage.Data.CallOnUpdate();
        }
    }
}