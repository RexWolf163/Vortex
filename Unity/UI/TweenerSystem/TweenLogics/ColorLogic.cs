using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.TweenerSystem.TweenLogics
{
    [Serializable]
    public class ColorLogic : TweenLogic
    {
        [SerializeField] private Image[] targets = new Image[0];
        [SerializeField] private Text[] targetsTexts = new Text[0];
        [SerializeField] private TextMeshPro[] targetsTextMP = new TextMeshPro[0];
        [SerializeField] private TextMeshProUGUI[] targetsTextUGUI = new TextMeshProUGUI[0];
        [SerializeField] private SpriteRenderer[] spriteRenderers = new SpriteRenderer[0];
        [SerializeField] private Color startColor = new(1f, 1f, 1f, 0f);
        [SerializeField] private Color endColor = Color.white;

        protected override void SetValue(float value)
        {
            foreach (var graphics in targets) graphics.color = Color.Lerp(startColor, endColor, value);
            foreach (var graphics in targetsTexts) graphics.color = Color.Lerp(startColor, endColor, value);
            foreach (var graphics in targetsTextMP) graphics.color = Color.Lerp(startColor, endColor, value);
            foreach (var graphics in targetsTextUGUI) graphics.color = Color.Lerp(startColor, endColor, value);
            foreach (var graphics in spriteRenderers) graphics.color = Color.Lerp(startColor, endColor, value);
        }

        protected override void SwitchOn()
        {
            foreach (var graphics in targets) graphics.gameObject.SetActive(true);
            foreach (var graphics in targetsTexts) graphics.gameObject.SetActive(true);
            foreach (var graphics in targetsTextMP) graphics.gameObject.SetActive(true);
            foreach (var graphics in targetsTextUGUI) graphics.gameObject.SetActive(true);
            foreach (var graphics in spriteRenderers) graphics.gameObject.SetActive(true);
        }

        protected override void SwitchOff()
        {
            foreach (var graphics in targets) graphics.gameObject.SetActive(false);
            foreach (var graphics in targetsTexts) graphics.gameObject.SetActive(false);
            foreach (var graphics in targetsTextMP) graphics.gameObject.SetActive(false);
            foreach (var graphics in targetsTextUGUI) graphics.gameObject.SetActive(false);
            foreach (var graphics in spriteRenderers) graphics.gameObject.SetActive(false);
        }
    }
}