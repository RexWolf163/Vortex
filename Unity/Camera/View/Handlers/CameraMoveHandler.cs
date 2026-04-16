using AppScripts.Camera.Controllers;
using AppScripts.Camera.Model;
using UnityEngine;
using Vortex.Unity.UI.Misc;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace AppScripts.Camera.View.Handlers
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
                    Data.SetPosition(Data.Target);
                else
                {
                    _isMove = true;
                    _moveTween.Set(() => transform.position, (pos) => Data.SetPosition(pos), Data.Target, easeDuration)
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
    }
}