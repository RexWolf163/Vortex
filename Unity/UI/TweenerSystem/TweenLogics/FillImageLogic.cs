using System;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    /// <summary>
    /// Плавное изменение значения заполнения картинки через fillAmount
    /// </summary>
    [Serializable]
    public class FillImageLogic : TweenLogic
    {
        [SerializeField] private Image[] images;

        protected override void SetValue(float value)
        {
            foreach (var image in images)
                image.fillAmount = value;
        }

        protected override void SwitchOn()
        {
            foreach (var image in images)
                image.gameObject.SetActive(true);
        }

        protected override void SwitchOff()
        {
            foreach (var image in images)
                image.gameObject.SetActive(false);
        }
    }
}