using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Переключение скина для спайн анимации
    /// </summary>
    [Serializable]
    public class SpineSkinSwitch : StateItem
    {
        [SerializeField, ValueSelector("GetListSkins")]
        private string skin;

        [SerializeField, HideIf("@spine==null&&spineAnimation!=null")]
        private SkeletonGraphic spine;

        [SerializeField, HideIf("@spine!=null")]
        private SkeletonAnimation spineAnimation;

        public override void Set()
        {
            SetSkin(skin);
        }

        public override void DefaultState()
        {
        }

        private void SetSkin(string skinName)
        {
            var skeleton = spine?.Skeleton;
            if (skeleton != null)
            {
                ApplySkin(skeleton, skinName);
                spine.UpdateMesh();
            }

            skeleton = spineAnimation?.Skeleton;
            if (skeleton != null)
                ApplySkin(skeleton, skinName);
        }

        private static void ApplySkin(Skeleton skeleton, string skinName)
        {
            skeleton.SetSkin(skinName);
            skeleton.SetSlotsToSetupPose();
        }

#if UNITY_EDITOR

        public override string DropDownItemName => "Switch Spine Skin";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new SpineSkinSwitch
            {
                skin = skin,
                spine = spine,
                spineAnimation = spineAnimation,
            };
        }

        private string[] GetListSkins()
        {
            var data = spine != null
                ? spine.SkeletonData
                : spineAnimation != null
                    ? spineAnimation.SkeletonDataAsset?.GetSkeletonData(true)
                    : null;

            return data?.Skins.Select(s => s.Name).ToArray() ?? Array.Empty<string>();
        }

#endif
    }
}