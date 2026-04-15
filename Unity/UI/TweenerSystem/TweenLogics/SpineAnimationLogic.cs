using System;
using System.Collections.Generic;
using System.Linq;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
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
    /// </summary>
    [Serializable]
    public class SpineAnimationLogic : TweenLogic
    {
        private const string None = "[NONE]";

        [SerializeField] private SkeletonGraphic skeleton;

        [SerializeField, Range(0, 10)] private byte animationChannel = 1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBubble("Анимация в положении Back")]
        private string animationIdle0;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBubble("Анимация в положении Forward")]
        private string animationIdle1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBubble("Анимация перехода в Forward")]
        private string animationFrw;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBubble("Анимация перехода в Back")]
        private string animationBack;

        [SerializeField,
         InfoBubble(
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
                if (animationFrw.IsNullOrWhitespace() || animationFrw.Equals(None))
                    return false;
            }
            else
            {
                if (animationBack.IsNullOrWhitespace() || animationBack.Equals(None))
                    return false;
            }

            if (!skipIfNotEqual)
                return true;

            if (forwardDirection)
                return skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == animationIdle0);

            return skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == animationIdle1);
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
                skeleton.AnimationState.SetAnimation(animationChannel, animationFrw, false);
                if (!animationIdle1.IsNullOrWhitespace() && !animationIdle1.Equals(None))
                    skeleton.AnimationState.AddAnimation(animationChannel, animationIdle1, true, 0);
                else
                    skeleton.AnimationState.AddEmptyAnimation(animationChannel, 0, 0);
                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
            else if (_isForwardState && CheckStartAnimationForDirection(false))
            {
                skeleton.AnimationState.SetAnimation(animationChannel, animationBack, false);
                if (!animationIdle0.IsNullOrWhitespace() && !animationIdle0.Equals(None))
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

            if (!animationIdle0.IsNullOrWhitespace() && !animationIdle0.Equals(None))
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

            if (!animationIdle1.IsNullOrWhitespace() && !animationIdle1.Equals(None))
                skeleton.AnimationState.SetAnimation(animationChannel, animationIdle1, true);
            else
                skeleton.AnimationState.SetEmptyAnimation(animationChannel, 0);
        }

#if UNITY_EDITOR
        private string[] GetListAnimations()
        {
            var list = skeleton?.SkeletonData.Animations.Select(a => a.Name).ToList() ?? new List<string>();
            list.Insert(0, None);
            return list.ToArray();
        }

#endif
    }
}