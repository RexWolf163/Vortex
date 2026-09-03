using System;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Выставляет Order отличный от 0 при активации. Default - возвращает на 0
    /// </summary>
    [Serializable]
    public class MeshRendererOrderSwitch : StateItem
    {
        [SerializeField, Min(1)] private int order = 1;
        [SerializeField] private MeshRenderer[] meshRenderers;

        public override void Set()
        {
            foreach (var meshRenderer in meshRenderers)
                meshRenderer.sortingOrder = order;
        }

        public override void DefaultState()
        {
            foreach (var meshRenderer in meshRenderers)
                meshRenderer.sortingOrder = 0;
        }
        
        public override bool IsValid()
        {
            foreach (var link in meshRenderers)
            {
                if (link == null)
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR

        public override string DropDownItemName => "Switch MeshRenderer Order";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new MeshRendererOrderSwitch
            {
                order = order,
                meshRenderers = meshRenderers.DeepCopy(),
            };
        }

#endif
    }
}