using System;
using UnityEngine;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    /// <summary>
    /// Активация твиннера TweenerHub
    /// </summary>
    [Serializable]
    public class TweenerHubSwitch : StateItem
    {
        [SerializeField] private TweenerHub tweener;

        public override void Set()
        {
            if (tweener == null) return;
            tweener.Forward();
        }

        public override void DefaultState()
        {
            if (tweener == null) return;
            tweener.Back();
        }

#if UNITY_EDITOR

        public override string DropDownItemName => "Switch Tweener Hub";
        public override string DropDownGroupName => "Tweeners";
        public override StateItem Clone()
        {
            return new TweenerHubSwitch
            {
                tweener = tweener
            };
        }
#endif
    }
}