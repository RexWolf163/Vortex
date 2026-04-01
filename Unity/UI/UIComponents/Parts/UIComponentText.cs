using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public class UIComponentText : UIComponentPart
    {
        [InfoBox("Можно выбрать один из вариантов или все сразу")] [SerializeField]
        private Text textField;

        [SerializeField] private TextMeshPro textMPField;
        [SerializeField] private TextMeshProUGUI textMPUiField;

#if UNITY_EDITOR
        [OnInspectorInit]
        private void Search()
        {
            if (textField == null)
                textField = GetComponent<Text>();
            if (textMPField == null)
                textMPField = GetComponent<TextMeshPro>();
            if (textMPUiField == null)
                textMPUiField = GetComponent<TextMeshProUGUI>();
        }
#endif
        public void PutData(string text)
        {
            if (textField != null)
                textField.text = text;
            if (textMPField != null)
                textMPField.text = text;
            if (textMPUiField != null)
                textMPUiField.text = text;
        }

        private void OnDestroy() => PutData("");

        public string GetValue()
        {
            if (textField != null)
                return textField.text;
            if (textMPField != null)
                return textMPField.text;
            if (textMPUiField != null)
                return textMPUiField.text;
            return String.Empty;
        }
    }
}