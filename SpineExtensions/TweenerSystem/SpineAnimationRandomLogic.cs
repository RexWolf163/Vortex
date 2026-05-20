using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;
using Random = UnityEngine.Random;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Запускает анимацию спайна.
    /// Если активен флаг, то запускает анимацию только при активной указанной анимации.
    ///
    /// Логика работает по бинарному принципу жесткого переключателя.
    /// Если передается 0, то выставляется animationIdle0
    /// Если передается 1 - animationIdle1
    /// Если любое промежуточное значение - animationFrw или animationBack в зависимости от _isForward
    ///
    /// Если анимация не указана, не воздействует на spine
    ///
    /// Анимации задаются списками из которых конкретная анимация выбирается случайным образом
    /// </summary>
    [Serializable]
    public class SpineAnimationRandomLogic : TweenLogic
    {
        [SerializeField] private SkeletonGraphic skeleton;

        [SerializeField, Range(0, 10)] private byte animationChannel = 1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация в положении Back")]
        private string[] animationsIdle0;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация в положении Forward")]
        private string[] animationsIdle1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация перехода в Forward")]
        private string[] animationsFrw;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация перехода в Back")]
        private string[] animationsBack;

        [SerializeField,
         InfoBox(
             "Играть анимацию только на совпадающей исходной анимации. Если не совпадает - не вызывать изменений")]
        private bool skipIfNotEqual;

        /// <summary>
        /// Проверка на условие запуска анимации
        /// </summary>
        /// <param name="forwardDirection"></param>
        /// <returns></returns>
        private bool CheckStartAnimationForDirection(bool forwardDirection)
        {
            if (forwardDirection)
            {
                if (animationsFrw == null || animationsFrw.Length == 0)
                    return false;
            }
            else
            {
                if (animationsBack == null || animationsBack.Length == 0)
                    return false;
            }

            if (!skipIfNotEqual)
                return true;

            if (forwardDirection)
                return animationsIdle0.Any(animationIdle0 =>
                    skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == animationIdle0));

            return animationsIdle1.Any(animationIdle1 =>
                skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == animationIdle1));
        }

        private string GetAnimation(string[] animations)
        {
            if (animations == null || animations.Length == 0)
                return null;
            return animations[Random.Range(0, animations.Length)];
        }

        private bool _isForwardState;
        private bool _isRunningState;

        protected override void SetValue(float value)
        {
            if (value == 0f) _isForwardState = false;

            if (value >= 1f) _isForwardState = true;

            if (value is <= 0 or >= 1f || _isRunningState) return;

            if (!_isForwardState && CheckStartAnimationForDirection(true))
            {
                var animationFrw = GetAnimation(animationsFrw);
                if (animationFrw == null)
                    return;
                skeleton.AnimationState.SetAnimation(animationChannel, animationFrw, false);
                var animationIdle1 = GetAnimation(animationsIdle1);
                if (!animationIdle1.IsNullOrWhitespace())
                    skeleton.AnimationState.AddAnimation(animationChannel, animationIdle1, true, 0);
                else
                    skeleton.AnimationState.AddEmptyAnimation(animationChannel, 0, 0);

                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
            else if (_isForwardState && CheckStartAnimationForDirection(false))
            {
                var animationBack = GetAnimation(animationsBack);
                if (animationBack == null)
                    return;
                skeleton.AnimationState.SetAnimation(animationChannel, animationBack, false);
                var animationIdle0 = GetAnimation(animationsIdle1);
                if (!animationIdle0.IsNullOrWhitespace())
                    skeleton.AnimationState.AddAnimation(animationChannel, animationIdle0, true, 0);
                else
                    skeleton.AnimationState.AddEmptyAnimation(animationChannel, 0, 0);

                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
        }

        protected override void SwitchOn() => skeleton.gameObject.SetActive(true);

        protected override void SwitchOff() => skeleton.gameObject.SetActive(false);

        protected override void OnStart()
        {
            if (_isRunningState)
            {
                _isRunningState = false;
                return;
            }

            var animationIdle0 = GetAnimation(animationsIdle1);
            if (!animationIdle0.IsNullOrWhitespace())
                skeleton.AnimationState.SetAnimation(animationChannel, animationIdle0, true);
            else
                skeleton.AnimationState.SetEmptyAnimation(animationChannel, 0);
        }

        protected override void OnEnd()
        {
            if (_isRunningState)
            {
                _isRunningState = false;
                return;
            }

            var animationIdle1 = GetAnimation(animationsIdle1);
            if (!animationIdle1.IsNullOrWhitespace())
                skeleton.AnimationState.SetAnimation(animationChannel, animationIdle1, true);
            else
                skeleton.AnimationState.SetEmptyAnimation(animationChannel, 0);
        }

#if UNITY_EDITOR
        private string[] GetListAnimations()
        {
            return skeleton?.SkeletonData.Animations.Select(a => a.Name).ToArray() ?? Array.Empty<string>();
        }

#endif
    }
}