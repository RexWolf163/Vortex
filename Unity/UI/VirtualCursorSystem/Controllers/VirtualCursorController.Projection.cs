using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.UI.VirtualCursorSystem.Handlers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Controllers
{
    /// <summary>
    /// Экран→мир проекция: ленивый raycast по позиции курсора, кэш в пределах кадра, LIFO-камеры
    /// (активна последняя зарегистрированная — позволяет временный override). Пакет отдаёт сырой хит
    /// и не интерпретирует его.
    /// </summary>
    public static partial class VirtualCursorController
    {
        /// <summary>Запись реестра камер проекции: камера и параметры raycast'а для неё.</summary>
        internal class CameraData
        {
            /// <summary>Камера, через которую строится луч из экранной позиции курсора.</summary>
            public Camera Cam;

            /// <summary>Маска слоёв raycast'а проекции.</summary>
            public LayerMask ProjectionMask;

            /// <summary>Дальность raycast'а проекции.</summary>
            public float ProjectionDistance;
        }

        /// <summary>LIFO-реестр камер: активна последняя зарегистрированная.</summary>
        private static readonly List<CameraData> Cameras = new();

        /// <summary>Кадр, в котором посчитан кэш проекции; -1 — кэш сброшен.</summary>
        private static int _projectionFrame = -1;

        /// <summary>Кэш: был ли хит в кадре <see cref="_projectionFrame"/>.</summary>
        private static bool _hasHit;

        /// <summary>Кэш: результат raycast'а в кадре <see cref="_projectionFrame"/>.</summary>
        private static RaycastHit _hit;

        /// <summary>
        /// Активная камера LIFO-реестра (последняя зарегистрированная через
        /// <c>CameraProvider</c>). Используется для <c>Projection</c> И для
        /// world-целей фокус-навигации (<see cref="FocusTargetComponent"/>
        /// с <c>TargetKind.World</c> проектирует Transform через эту камеру).
        /// </summary>
        internal static CameraData ActiveCamera => Cameras.Count > 0 ? Cameras[^1] : null;

        /// <summary>
        /// Зарегистрировать камеру проекции поверх остальных (повторная регистрация переносит её наверх
        /// с новыми параметрами). Зовётся <see cref="CameraProvider"/> на OnEnable.
        /// </summary>
        internal static void RegisterCamera(Camera camera, LayerMask mask, float distance)
        {
            if (camera == null) return;
            RemoveCamera(camera);

            Cameras.Add(new CameraData
                { Cam = camera, ProjectionDistance = distance, ProjectionMask = mask });
            InvalidateProjection();
        }

        /// <summary>Удалить все записи реестра с этой камерой.</summary>
        private static void RemoveCamera(Camera camera)
        {
            for (var i = Cameras.Count - 1; i >= 0; i--)
            {
                var cameraData = Cameras[i];
                if (cameraData.Cam == camera)
                    Cameras.RemoveAt(i);
            }
        }

        /// <summary>Снять камеру из реестра. Зовётся <see cref="CameraProvider"/> на OnDisable.</summary>
        internal static void UnregisterCamera(Camera camera)
        {
            RemoveCamera(camera);
            InvalidateProjection();
        }

        /// <summary>
        /// Сбросить кэш проекции — пересчёт при следующем запросе. Контроллер сбрасывает его сам при смене позиции
        /// курсора и реестра камер; вручную — когда сдвинулась геометрия сцены.
        /// </summary>
        public static void InvalidateProjection() => _projectionFrame = -1;

        /// <summary>Экран→мир: точка и объект под курсором. false на промах/без камеры.</summary>
        public static bool TryGetWorldHit(out RaycastHit hit)
        {
            EnsureProjection();
            hit = _hit;
            return _hasHit;
        }

        /// <summary>Точка на поверхности под курсором; null на промах.</summary>
        public static Vector3? GetWorldProjection() => TryGetWorldHit(out var hit) ? hit.point : (Vector3?)null;

        /// <summary>
        /// Посчитать проекцию не чаще раза в кадр (кэш сбрасывается сменой позиции курсора и реестра камер):
        /// луч из позиции курсора через активную камеру. Без камеры или модели — промах.
        /// </summary>
        private static void EnsureProjection()
        {
            if (_projectionFrame == Time.frameCount)
                return;
            _projectionFrame = Time.frameCount;

            var cam = ActiveCamera;
            if (cam == null || cam.Cam == null || _model == null)
            {
                _hasHit = false;
                return;
            }

            var ray = cam.Cam.ScreenPointToRay(_model.ScreenPosition.Value);
            _hasHit = Physics.Raycast(ray, out _hit, cam.ProjectionDistance, cam.ProjectionMask);
        }
    }
}