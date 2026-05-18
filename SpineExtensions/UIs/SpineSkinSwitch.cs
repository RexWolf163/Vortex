using System;
using System.Linq;
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

        [SerializeField] private SkeletonGraphic spine;

        public override void Set()
        {
            SetSkin(skin);
        }

        public override void DefaultState()
        {
        }

        private void SetSkin(string skinName)
        {
            spine.Skeleton.SetSkin(skinName);
            spine.Skeleton.SetSlotsToSetupPose();
            spine.UpdateMesh();
        }

#if UNITY_EDITOR

        public override string DropDownItemName => "Switch Spine Skin";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new SpineSkinSwitch
            {
                skin = skin,
                spine = spine
            };
        }

        private string[] GetListSkins() =>
            spine?.SkeletonData.Skins.Select(a => a.Name).ToArray() ?? Array.Empty<string>();

#endif
    }
}