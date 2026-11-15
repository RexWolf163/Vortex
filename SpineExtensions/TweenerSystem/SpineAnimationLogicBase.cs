using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Базовый класс одиночной анимации Spine — общий для SkeletonGraphic (UGUI) и
    /// SkeletonAnimation (MeshRenderer). Конкретные подклассы отличаются только типом поля skeleton;
    /// данные скелета идут через интерфейс Spine <see cref="IHasSkeletonDataAsset"/>, состояние анимации —
    /// через <see cref="SpineCompat.GetAnimationState"/>: в Spine 4.3 его ведёт не сам SkeletonGraphic,
    /// а соседний компонент анимации.
    ///
    /// Логика работает по бинарному принципу жесткого переключателя.
    /// Если передается 0, то выставляется animationIdle0
    /// Если передается 1 - animationIdle1
    /// Если любое промежуточное значение - animationFrw или animationBack в зависимости от _isForward
    ///
    /// Если анимация не указана, не воздействует на spine.
    /// </summary>
    public abstract class SpineAnimationLogicBase<TSkeleton> : TweenLogic
        where TSkeleton : MonoBehaviour, IHasSkeletonDataAsset
    {
        private const string None = "[NONE]";

        [ClassFilter(typeof(IHasSkeletonDataAsset))] [SerializeField]
        protected TSkeleton skeleton;

        /// <summary>
        /// Состояние анимации скелета — независимо от версии Spine
        /// </summary>
        private Spine.AnimationState State => SpineCompat.GetAnimationState(skeleton);

        [SerializeField, Range(0, 10)] private byte animationChannel = 1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация в положении Back")]
        private string animationIdle0;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация в положении Forward")]
        private string animationIdle1;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация перехода в Forward")]
        private string animationFrw;

        [SerializeField, ValueSelector("GetListAnimations")] [InfoBox("Анимация перехода в Back")]
        private string animationBack;

        [SerializeField,
         InfoBox(
             "Играть анимацию только на совпадающей исходной анимации. Если не совпадает - не вызывать изменений")]
        private bool skipIfNotEqual;

        private bool _isForwardState;
        private bool _isRunningState;

        /// <summary>
        /// Проверка на условие запуска анимации
        /// </summary>
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
                return State.Tracks.Any(t => t?.Animation?.Name == animationIdle0);

            return State.Tracks.Any(t => t?.Animation?.Name == animationIdle1);
        }

        protected override void SetValue(float value)
        {
            if (value == 0f) _isForwardState = false;

            if (value >= 1f) _isForwardState = true;

            if (value is <= 0 or >= 1f || _isRunningState) return;

            if (!_isForwardState && CheckStartAnimationForDirection(true))
            {
                State.SetAnimation(animationChannel, animationFrw, false);
                if (!animationIdle1.IsNullOrWhitespace() && !animationIdle1.Equals(None))
                    State.AddAnimation(animationChannel, animationIdle1, true, 0);
                else
                    State.AddEmptyAnimation(animationChannel, 0, 0);
                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
            else if (_isForwardState && CheckStartAnimationForDirection(false))
            {
                State.SetAnimation(animationChannel, animationBack, false);
                if (!animationIdle0.IsNullOrWhitespace() && !animationIdle0.Equals(None))
                    State.AddAnimation(animationChannel, animationIdle0, true, 0);
                else
                    State.AddEmptyAnimation(animationChannel, 0, 0);

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
                State.SetAnimation(animationChannel, animationIdle0, true);
            else
                State.SetEmptyAnimation(animationChannel, 0);
        }

        protected override void OnEnd()
        {
            if (_isRunningState)
            {
                _isRunningState = false;
                return;
            }

            if (!animationIdle1.IsNullOrWhitespace() && !animationIdle1.Equals(None))
                State.SetAnimation(animationChannel, animationIdle1, true);
            else
                State.SetEmptyAnimation(animationChannel, 0);
        }

#if UNITY_EDITOR
        private string[] GetListAnimations()
        {
            var data = skeleton != null && skeleton.SkeletonDataAsset != null
                ? skeleton.SkeletonDataAsset.GetSkeletonData(true)
                : null;
            var list = data?.Animations.Select(a => a.Name).ToList() ?? new List<string>();
            list.Insert(0, None);
            return list.ToArray();
        }
#endif
    }
}