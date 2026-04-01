using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    /// <summary>
    /// Меняет прозрачность канваса через компонент CanvasGroup.
    /// Управляет raycast
    ///
    /// В конечной точке твина, raycast = raycastOnEnd
    /// В начальной и промежуточных raycast = raycastOnStart (ассиметрично)
    /// </summary>
    [Serializable]
    public class CanvasOpacityLogic : TweenLogic
    {
        [SerializeField, InfoBubble("Raycast в начале и в движении")]
        private bool raycastOnStart = true;

        [SerializeField, InfoBubble("Raycast в конце")]
        private bool raycastOnEnd = true;

        [SerializeField] private CanvasGroup[] canvasGroup;

        protected override void SetValue(float value)
        {
            if (!raycastOnStart && value < 1f)
                foreach (var group in canvasGroup)
                    group.blocksRaycasts = raycastOnStart;

            foreach (var group in canvasGroup)
                group.alpha = value;
        }

        protected override void SwitchOn()
        {
            foreach (var group in canvasGroup)
                group.gameObject.SetActive(true);
        }

        protected override void SwitchOff()
        {
            foreach (var group in canvasGroup)
                group.gameObject.SetActive(false);
        }

        protected override void OnStart()
        {
            foreach (var group in canvasGroup)
                group.blocksRaycasts = raycastOnStart;
        }

        protected override void OnEnd()
        {
            foreach (var group in canvasGroup)
                group.blocksRaycasts = raycastOnEnd;
        }
    }
}