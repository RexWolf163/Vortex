using System;
using UnityEngine;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.NaniExtensions.Misc
{
    /// <summary>
    /// Меняет сортировку спрайтов
    /// </summary>
    [Serializable]
    public class ZPositionSwitch : StateItem
    {
        [SerializeField] private SpriteRenderer[] targets;

        [SerializeField] private int targetZ;

        public override void Set()
        {
            foreach (var target in targets)
                target.sortingOrder = targetZ;
        }

        public override void DefaultState()
        {
            foreach (var target in targets)
                target.sortingOrder = 0;
        }

#if UNITY_EDITOR

        public override bool IsValid()
        {
            foreach (var link in targets)
            {
                if (link == null)
                    return false;
            }

            return true;
        }

        public override string DropDownItemName => "Z Position";
        public override string DropDownGroupName => "Graphics";

        public override StateItem Clone()
        {
            return new ZPositionSwitch
            {
                targets = targets,
                targetZ = targetZ
            };
        }
#endif
    }
}