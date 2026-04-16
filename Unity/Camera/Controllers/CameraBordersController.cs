using UnityEngine;
using Vortex.Unity.Camera.Model;
using Object = System.Object;

namespace Vortex.Unity.Camera.Controllers
{
    /// <summary>
    /// Контроллер управления границами камеры.
    /// Можно задавать несколько RectTransform как границы.
    /// При нескольких границах, камера ограничивается на последней.
    /// Если последняя граница удалена, камера переключится на предыдущую.
    /// </summary>
    public static class CameraBordersController
    {
        private static readonly Object Key = new();

        /// <summary>
        /// Добавить новую границу
        /// </summary>
        /// <param name="data"></param>
        /// <param name="borders"></param>
        public static void AddBorder(this CameraModel data, RectTransform borders)
        {
            if (data.borders.Contains(borders))
                return;
            data.borders.Add(borders);

            data.CallOnUpdate();
        }

        /// <summary>
        /// Удалить границу
        /// </summary>
        /// <param name="data"></param>
        /// <param name="borders"></param>
        public static void RemoveBorder(this CameraModel data, RectTransform borders)
        {
            data.borders.Remove(borders);

            data.CallOnUpdate();
        }

        /// <summary>
        /// Удалить все границы для камеры
        /// </summary>
        /// <param name="data"></param>
        public static void ClearBorders(this CameraModel data)
        {
            data.borders.Clear();

            data.CallOnUpdate();
        }
    }
}