using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Переключение анимации для спайна
    /// </summary>
    [Serializable]
    public class SpineAnimationSwitch : StateItem
    {
        [SerializeField, ValueSelector("GetListAnimations")]
        private string animationName;

        [SerializeField, HideIf("@spine==null&&spineAnimation!=null")]
        private SkeletonGraphic spine;

        [SerializeField, HideIf("@spine!=null")]
        private SkeletonAnimation spineAnimation;

        [SerializeField, Range(0, 10)] private int channel = 0;
        [SerializeField] private bool loop = true;

        public override void Set() => SetAnimation();

        public override void DefaultState()
        {
            //Нет отключения на default
        }

        private void SetAnimation()
        {
            if (spine != null)
                spine.AnimationState.SetAnimation(channel, animationName, loop);

            if (spineAnimation != null)
                spineAnimation.AnimationState.SetAnimation(channel, animationName, loop);
        }

#if UNITY_EDITOR

        public override string DropDownItemName => "Switch Spine Skin";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new SpineAnimationSwitch
            {
                animationName = animationName,
                spine = spine,
                spineAnimation = spineAnimation,
                channel = channel,
                loop = loop
            };
        }

        private string[] GetListAnimations()
        {
            var data = spine != null
                ? spine.SkeletonData
                : spineAnimation != null
                    ? spineAnimation.SkeletonDataAsset?.GetSkeletonData(true)
                    : null;

            return data?.Animations.Select(s => s.Name).ToArray() ?? Array.Empty<string>();
        }

#endif
    }
}