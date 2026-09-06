using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Экран→мир проекция: ленивый raycast по позиции курсора, кэш в пределах кадра, LIFO-камеры
    /// (активна последняя зарегистрированная — позволяет временный override). Пакет отдаёт сырой хит
    /// и не интерпретирует его.
    /// </summary>
    public static partial class VirtualCursorController
    {
        private static readonly List<Camera> _cameras = new();
        private static LayerMask _projectionMask = ~0;
        private static float _projectionDistance = 1000f;

        private static int _projectionFrame = -1;
        private static bool _hasHit;
        private static RaycastHit _hit;

        private static Camera ActiveCamera => _cameras.Count > 0 ? _cameras[^1] : null;

        /// <summary>Параметры проекции (маска слоёв, дистанция). Зовётся бутстрапом/провайдером.</summary>
        public static void ConfigureProjection(LayerMask mask, float distance)
        {
            _projectionMask = mask;
            _projectionDistance = distance;
            _projectionFrame = -1;
        }

        internal static void RegisterCamera(Camera camera)
        {
            if (camera == null) return;
            _cameras.Remove(camera);
            _cameras.Add(camera);
        }

        internal static void UnregisterCamera(Camera camera) => _cameras.Remove(camera);

        /// <summary>Сбросить кэш проекции — пересчёт при следующем запросе (напр. геометрия сдвинулась).</summary>
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

        private static void EnsureProjection()
        {
            if (_projectionFrame == Time.frameCount)
                return;
            _projectionFrame = Time.frameCount;

            var cam = ActiveCamera;
            if (cam == null || _model == null)
            {
                _hasHit = false;
                return;
            }

            var ray = cam.ScreenPointToRay(_model.ScreenPosition.Value);
            _hasHit = Physics.Raycast(ray, out _hit, _projectionDistance, _projectionMask);
        }
    }
}
