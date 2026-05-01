using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    [Serializable]
    public class PivotLogic : TweenLogic
    {
        [SerializeField, InfoBox("Позиция при скрытие")]
        private Vector2 startPos;

        [SerializeField, InfoBox("Позиция при показе")]
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