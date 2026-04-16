using UnityEngine;
using Vortex.Unity.Camera.Controllers;
using Vortex.Unity.Camera.Model;
using Vortex.Unity.UI.Misc;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace Vortex.Unity.Camera.View.Handlers
{
    /// <summary>
    /// Контроллер приведения камеры к цели
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraMoveHandler : DataStorageView<CameraModel>
    {
        /// <summary>
        /// Время пролета камеры если она далеко
        /// </summary>
        [SerializeField, Range(0f, 3f)] private float easeDuration = 1f;

        [SerializeField] private EaseType easeType = EaseType.Linear;

        /// <summary>
        /// Радиус отклонения, где смещение идет без асинхронного подведения камеры,
        /// а мгновенно
        /// </summary>
        [SerializeField, Range(0f, 30f)] private float followingRange = 5f;

        private Vector2 _cachedTarget;
        private bool IsFollowing => ((Vector2)transform.position - _cachedTarget).magnitude <= followingRange;

        private readonly AsyncTween _moveTween = new();

        private bool _lock;
        private bool _isMove;

        protected override void Init()
        {
            base.Init();
            _moveTween.Kill();
            if (Data == null)
                return;
            _cachedTarget = Data.Target;
        }

        protected override void DeInit()
        {
            base.DeInit();
            _moveTween.Kill();
        }

        protected override void OnDataUpdated()
        {
            if (_lock || _isMove) return;

            if (Data.FocusedObjects == null || Data.FocusedObjects.Count == 0)
                return;
            if (_cachedTarget == Data.Target && ((Vector2)transform.position - _cachedTarget).magnitude < 0.1f)
                return;

            _cachedTarget = Data.Target;

            _lock = true;
            try
            {
                if (IsFollowing)
                    CalcPosition(Data.Target);
                else
                {
                    _isMove = true;
                    _moveTween.Set(() => transform.position, CalcPosition, Data.Target, easeDuration)
                        .SetEase(easeType)
                        .OnComplete(() => _isMove = false)
                        .OnKill(() => _isMove = false)
                        .Run();
                }
            }
            finally
            {
                _lock = false;
            }
        }

        /// <summary>
        /// Расчет положения камеры с учетом Borders
        /// </summary>
        /// <param name="pos"></param>
        private void CalcPosition(Vector2 pos)
        {
            if (!Data.IsBordered || Data.Borders.Count == 0)
            {
                Data.SetPosition(pos);
                return;
            }

            var dX = Data.CameraRect.Value.x / 2;
            var dY = Data.CameraRect.Value.y / 2;
            var borderRect = Data.Borders[^1];
            var corners = new Vector3[4];
            borderRect.GetWorldCorners(corners);

            var borders = new Rect(
                corners[0].x + dX,
                corners[0].y + dY,
                corners[2].x - corners[0].x - 2 * dX,
                corners[2].y - corners[0].y - 2 * dY);

            pos.x = Mathf.Clamp(pos.x, borders.xMin, borders.xMax);
            pos.y = Mathf.Clamp(pos.y, borders.yMin, borders.yMax);
            Data.SetPosition(pos);
        }
    }
}