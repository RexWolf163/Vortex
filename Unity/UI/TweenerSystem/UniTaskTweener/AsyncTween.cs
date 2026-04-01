using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Unity.UI.TweenerSystem.UniTaskTweener
{
    /// <summary>
    /// Лёгкий твинер на UniTask.
    /// Каждый экземпляр — один независимый поток анимации.
    /// Fluent API: tween.Set(...).SetEase(...).OnComplete(...).Run()
    /// </summary>
    public class AsyncTween
    {
        private CancellationTokenSource _cts;

        // Параметры анимации (задаются через Set)
        private Action _instantApply;

        // Параметры fluent-цепочки
        private EaseType _ease = EaseType.Linear;
        private AnimationCurve _customCurve;
        private Action _onComplete;
        private Action<float> _onUpdate;
        private CancellationToken _externalToken;

        /// <summary>
        /// Текущий прогресс (от 0 до 1)
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Анимация активна
        /// </summary>
        public bool IsPlaying => _cts is { IsCancellationRequested: false };

        private uint _id;

        #region Fluent API

        /// <summary>
        /// Задать функцию плавности
        /// </summary>
        public AsyncTween SetEase(EaseType ease)
        {
            _ease = ease;
            _customCurve = null;
            return this;
        }

        /// <summary>
        /// Задать кривую плавности через AnimationCurve
        /// </summary>
        public AsyncTween SetEase(AnimationCurve curve)
        {
            _customCurve = curve;
            return this;
        }

        /// <summary>
        /// Колбэк по завершении (не вызывается при Kill)
        /// </summary>
        public AsyncTween OnComplete(Action action)
        {
            _onComplete = action;
            return this;
        }

        /// <summary>
        /// Колбэк каждый кадр с текущим прогрессом (0..1)
        /// </summary>
        public AsyncTween OnUpdate(Action<float> action)
        {
            _onUpdate = action;
            return this;
        }

        /// <summary>
        /// Привязать внешний CancellationToken
        /// </summary>
        public AsyncTween SetToken(CancellationToken token)
        {
            _externalToken = token;
            return this;
        }

        #endregion

        #region Set Methods

        private enum AnimType
        {
            Float,
            Vector2,
            Vector3,
            Color
        }

        private AnimType _animType;
        private Func<float> _getFloat;
        private Action<float> _setFloat;
        private float _targetFloat;
        private Func<Vector2> _getV2;
        private Action<Vector2> _setV2;
        private Vector2 _targetV2;
        private Func<Vector3> _getV3;
        private Action<Vector3> _setV3;
        private Vector3 _targetV3;
        private Func<Color> _getColor;
        private Action<Color> _setColor;
        private Color _targetColor;
        private float _duration;

        /// <summary>
        /// Настроить анимацию float
        /// </summary>
        public AsyncTween Set(Func<float> getter, Action<float> setter, float target, float duration)
        {
            _animType = AnimType.Float;
            _getFloat = getter;
            _setFloat = setter;
            _targetFloat = target;
            _duration = duration;
            _instantApply = () => setter(target);
            return this;
        }

        /// <summary>
        /// Настроить анимацию Vector2
        /// </summary>
        public AsyncTween Set(Func<Vector2> getter, Action<Vector2> setter, Vector2 target, float duration)
        {
            _animType = AnimType.Vector2;
            _getV2 = getter;
            _setV2 = setter;
            _targetV2 = target;
            _duration = duration;
            _instantApply = () => setter(target);
            return this;
        }

        /// <summary>
        /// Настроить анимацию Vector3
        /// </summary>
        public AsyncTween Set(Func<Vector3> getter, Action<Vector3> setter, Vector3 target, float duration)
        {
            _animType = AnimType.Vector3;
            _getV3 = getter;
            _setV3 = setter;
            _targetV3 = target;
            _duration = duration;
            _instantApply = () => setter(target);
            return this;
        }

        /// <summary>
        /// Настроить анимацию Color
        /// </summary>
        public AsyncTween Set(Func<Color> getter, Action<Color> setter, Color target, float duration)
        {
            _animType = AnimType.Color;
            _getColor = getter;
            _setColor = setter;
            _targetColor = target;
            _duration = duration;
            _instantApply = () => setter(target);
            return this;
        }

        #endregion

        #region Run

        /// <summary>
        /// Запустить настроенную анимацию
        /// </summary>
        public AsyncTween Run()
        {
            var id = ++_id;
            var instantApply = _instantApply;
            var onComplete = _onComplete;
            var onUpdate = _onUpdate;
            var externalToken = _externalToken;
            var duration = _duration;
            var animType = _animType;

            var curve = _customCurve;
            var ease = _ease;
            Func<float, float> easeFn = curve != null
                ? t => curve.Evaluate(t)
                : t => Easing.Evaluate(ease, t);

            // Захватываем все ссылки в локальные переменные до ResetParams
            var getFloat = _getFloat;
            var setFloat = _setFloat;
            var targetFloat = _targetFloat;
            var getV2 = _getV2;
            var setV2 = _setV2;
            var targetV2 = _targetV2;
            var getV3 = _getV3;
            var setV3 = _setV3;
            var targetV3 = _targetV3;
            var getColor = _getColor;
            var setColor = _setColor;
            var targetColor = _targetColor;

            Func<CancellationToken, UniTask> animation = animType switch
            {
                AnimType.Float => token => Animate(getFloat(), targetFloat, duration,
                    (from, to, t) => setFloat(Mathf.LerpUnclamped(from, to, t)),
                    easeFn, onUpdate, token),
                AnimType.Vector2 => token => Animate(getV2(), targetV2, duration,
                    (from, to, t) => setV2(Vector2.LerpUnclamped(from, to, t)),
                    easeFn, onUpdate, token),
                AnimType.Vector3 => token => Animate(getV3(), targetV3, duration,
                    (from, to, t) => setV3(Vector3.LerpUnclamped(from, to, t)),
                    easeFn, onUpdate, token),
                AnimType.Color => token => Animate(getColor(), targetColor, duration,
                    (from, to, t) => setColor(Color.LerpUnclamped(from, to, t)),
                    easeFn, onUpdate, token),
                _ => null
            };

            ResetParams();

            if (duration <= 0f || animation == null)
            {
                Kill();
                instantApply?.Invoke();
                onComplete?.Invoke();
                return this;
            }

            if (Settings.Data().AsyncTweenerDebugMode)
                Debug.Log($"[Async Tweener] Animation #{id} run");
            ExecuteAsync(animation, onComplete, externalToken, id).Forget();
            return this;
        }

        #endregion

        #region Core

        /// <summary>
        /// Универсальный цикл анимации
        /// </summary>
        private async UniTask Animate<T>(
            T from, T target, float duration,
            Action<T, T, float> apply,
            Func<float, float> easeFn,
            Action<float> onUpdate,
            CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
                Progress = Mathf.Clamp01(elapsed / duration);
                apply(from, target, easeFn(Progress));
                onUpdate?.Invoke(Progress);
            }

            Progress = 1f;
            apply(from, target, 1f);
            onUpdate?.Invoke(1f);
        }

        /// <summary>
        /// Ядро запуска
        /// </summary>
        private async UniTask ExecuteAsync(
            Func<CancellationToken, UniTask> animation,
            Action onComplete,
            CancellationToken externalToken,
            uint id)
        {
            Kill();

            _cts = new CancellationTokenSource();
            using var linked = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken)
                : null;
            var token = linked?.Token ?? _cts.Token;

            try
            {
                await animation(token);
                onComplete?.Invoke();
                if (Settings.Data().AsyncTweenerDebugMode)
                    Debug.Log($"[AsyncTween] Animation #{id} completed");
            }
            catch (OperationCanceledException)
            {
                if (Settings.Data().AsyncTweenerDebugMode)
                    Debug.LogWarning($"[Async Tweener] Animation #{id} Exception catched");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Сброс параметров fluent-цепочки
        /// </summary>
        private void ResetParams()
        {
            _ease = EaseType.Linear;
            _customCurve = null;
            _onComplete = null;
            _onUpdate = null;
            _externalToken = default;
            _instantApply = null;
            _getFloat = null;
            _setFloat = null;
            _getV2 = null;
            _setV2 = null;
            _getV3 = null;
            _setV3 = null;
            _getColor = null;
            _setColor = null;
        }

        /// <summary>
        /// Остановить анимацию
        /// </summary>
        public void Kill()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            if (Settings.Data().AsyncTweenerDebugMode)
                Debug.LogWarning($"[AsyncTween] Animation #{_id} was Canceled");
        }

        #endregion
    }
}