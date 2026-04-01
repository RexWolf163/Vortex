using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    [Serializable]
    public class PivotLogic : TweenLogic
    {
        [SerializeField, InfoBubble("Позиция при скрытие")]
        private Vector2 startPos;

        [SerializeField, InfoBubble("Позиция при показе")]
        private Vector2 endPos;

        [SerializeField] private RectTransform rect;

        protected override void SetValue(float value)
        {
            rect.pivot = startPos + (endPos - startPos) * value;
        }

        protected override void SwitchOn()
        {
            rect.gameObject.SetActive(true);
        }

        protected override void SwitchOff()
        {
            rect.gameObject.SetActive(true);
        }
    }
}