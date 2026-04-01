using System;
using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    [Serializable]
    public class RectScaleLogic : TweenLogic
    {
        private enum ScaleMode
        {
            Both,
            X,
            Y
        }

        [SerializeField] private ScaleMode mode = ScaleMode.Both;

        [SerializeField] private RectTransform[] rects;

        protected override void SetValue(float value)
        {
            foreach (var rect in rects)
            {
                var old = rect.localScale;
                rect.localScale = new Vector3(mode == ScaleMode.Both || mode == ScaleMode.X ? value : old.x,
                    mode == ScaleMode.Both || mode == ScaleMode.Y ? value : old.y,
                    old.z);
            }
        }

        protected override void SwitchOn()
        {
            foreach (var rect in rects)
                rect.gameObject.SetActive(true);
        }

        protected override void SwitchOff()
        {
            foreach (var rect in rects)
                rect.gameObject.SetActive(false);
        }
    }
}