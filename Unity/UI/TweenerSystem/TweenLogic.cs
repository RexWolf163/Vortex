using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.TweenerSystem
{
    /// <summary>
    /// Логика изменения параметра
    /// </summary>
    [Serializable, HideReferenceObjectPicker, ClassLabel("@ClassName")]
    public abstract class TweenLogic
    {
        /// <summary>
        /// Задержка анимации в секундах
        /// </summary>
        [InfoBubble("Задержка анимации в секундах для Forward")] [SerializeField]
        protected float offset;

        /// <summary>
        /// Задержка анимации в секундах для Back
        /// </summary>
        [InfoBubble("Задержка анимации в секундах для Back")] [SerializeField]
        protected float offsetBack;

        [SerializeField] protected TweenPreset preset;

        private UniTask _tweenTask;
        private bool _isForward;
        private float _progress;
        private DateTime _startTween;
        private Action _onComplete;

        private CancellationTokenSource _cts;
        private CancellationToken Token => (_cts ??= new CancellationTokenSource()).Token;

        protected internal virtual void Init()
        {
            _isForward = false;
            _progress = 0f;
            var eased = preset.curve.Evaluate(0f);
            SetValue(eased);
            if (preset.offOnStartPoint)
                SwitchOff();
        }

        protected internal virtual void DeInit()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _tweenTask = default;
        }

        protected internal virtual void Forward(bool skip = false)
        {
            if (preset.offOnStartPoint)
                SwitchOn();
            if (!skip)
            {
                if (_isForward)
                    return;
                _isForward = true;
                if (_tweenTask.Status == UniTaskStatus.Pending)
                    _startTween = _startTween.AddSeconds(-(DateTime.UtcNow - _startTween).TotalSeconds + offset);
                else
                {
                    _startTween = DateTime.UtcNow.AddSeconds(offset);
                    _tweenTask = Tween(Token);
                }
            }
            else
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                _tweenTask = default;
                _isForward = true;
                _progress = 1f;
                var eased = preset.curve.Evaluate(1f);
                SetValue(eased);
                if (preset.offOnEndPoint)
                    SwitchOff();
            }
        }


        protected internal virtual void Back(bool skip = false)
        {
            if (preset.offOnEndPoint)
                SwitchOn();
            if (!skip)
            {
                if (!_isForward)
                    return;
                _isForward = false;
                if (_tweenTask.Status == UniTaskStatus.Pending)
                    _startTween = _startTween.AddSeconds(-(DateTime.UtcNow - _startTween).TotalSeconds + offsetBack);
                else
                {
                    _startTween = DateTime.UtcNow.AddSeconds(offsetBack);
                    _tweenTask = Tween(Token);
                }
            }
            else
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                _tweenTask = default;
                _isForward = false;
                _progress = 0f;
                var eased = preset.curve.Evaluate(0f);
                SetValue(eased);
                if (preset.offOnStartPoint)
                    SwitchOff();
            }
        }

        protected internal virtual void Pulse()
        {
            if (preset.offOnStartPoint)
                SwitchOn();
            if (_isForward)
            {
                if (_tweenTask.Status == UniTaskStatus.Pending)
                {
                    _onComplete = () => Back();
                    return;
                }

                Back();
                return;
            }

            Forward();
            _onComplete = () => Back();
        }

        protected string ClassName => GetType().Name;

        protected abstract void SetValue(float value);

        protected abstract void SwitchOn();
        protected abstract void SwitchOff();

        private async UniTask Tween(CancellationToken token)
        {
            try
            {
                while (_progress > 0 && !_isForward || _progress < 1 && _isForward)
                {
                    if (token.IsCancellationRequested)
                    {
                        _progress = _isForward ? 1 : 0;
                        return;
                    }

                    if (preset.duration > 0)
                    {
                        var progress =
                            Mathf.Clamp01((float)(DateTime.UtcNow - _startTween).TotalSeconds / preset.duration);
                        _progress = _isForward ? progress : 1 - progress;
                    }
                    else
                    {
                        _progress = _isForward ? 1 : 0;
                    }

                    var eased = preset.curve.Evaluate(_progress);
                    SetValue(eased);

                    await UniTask.Yield();
                }

                _tweenTask = default;
                var action = _onComplete;
                _onComplete = null;
                action?.Invoke();
                if (_isForward)
                    OnEnd();
                else
                    OnStart();
                if (!_isForward && preset.offOnStartPoint || _isForward && preset.offOnEndPoint)
                    SwitchOff();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Болванка под заполнение в наследнике для специфической логике в точке начала
        /// </summary>
        protected virtual void OnStart()
        {
        }

        /// <summary>
        /// Болванка под заполнение в наследнике для специфической логике в точке конца
        /// </summary>
        protected virtual void OnEnd()
        {
        }

#if UNITY_EDITOR
        [Button("Back", ButtonSizes.Large), HorizontalGroup("testButtons")]
        private void BackTest()
        {
            if (Application.isPlaying)
                Back();
            else
                Back(true);
        }

        [Button("Forward", ButtonSizes.Large), HorizontalGroup("testButtons")]
        private void ForwardTest()
        {
            if (Application.isPlaying)
                Forward();
            else
                Forward(true);
        }

        [Button("Pulse", ButtonSizes.Large), HorizontalGroup("testButtons"), HideInEditorMode]
        private void PulseTest() => Pulse();
#endif
    }
}