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

        public override void SetDirty()
        {
            Dirty(textField);
            Dirty(textMPField);
            Dirty(textMPUiField);
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

#if UNITY_EDITOR
        // Выравнивание и маска — системные методы инспектора UIComponent (блоки «Выравнивание текста», «Maskable»).
        /// <summary>
        /// Maskable части: первого заданного UI-поля (TMP UGUI, Text). <see cref="TextMeshPro"/> — 3D-текст, маскируемости
        /// UI не имеет. UI-полей нет — <c>null</c>.
        /// </summary>
        internal bool? GetMaskable()
        {
            if (textMPUiField != null)
                return textMPUiField.maskable;
            if (textField != null)
                return textField.maskable;
            return null;
        }

        /// <summary>Выставить maskable всем заданным UI-полям части. Изменение записывается в Undo.</summary>
        internal void SetMaskable(bool maskable)
        {
            if (textField != null) UnityEditor.Undo.RecordObject(textField, "Maskable");
            if (textMPUiField != null) UnityEditor.Undo.RecordObject(textMPUiField, "Maskable");
            if (textField != null)
                textField.maskable = maskable;
            if (textMPUiField != null)
                textMPUiField.maskable = maskable;
        }

        /// <summary>
        /// Выравнивание текста части: первого заданного поля (TMP UGUI, TMP, Text). Для <see cref="Text"/> — ближайшее
        /// из 9 якорей. Полей нет — <see cref="TextAlignmentOptions.TopLeft"/>.
        /// </summary>
        internal TextAlignmentOptions GetAlignment()
        {
            if (textMPUiField != null)
                return textMPUiField.alignment;
            if (textMPField != null)
                return textMPField.alignment;
            if (textField != null)
                return FromAnchor(textField.alignment);
            return TextAlignmentOptions.TopLeft;
        }

        /// <summary>
        /// Выставить выравнивание всем заданным полям части. TMP получает значение как есть, <see cref="Text"/> —
        /// ближайший из 9 якорей (justified/flush → влево, baseline → вниз, capline → вверх, geometry → центр).
        /// Изменение записывается в Undo.
        /// </summary>
        internal void SetAlignment(TextAlignmentOptions alignment)
        {
            if (textField != null) UnityEditor.Undo.RecordObject(textField, "Text Alignment");
            if (textMPField != null) UnityEditor.Undo.RecordObject(textMPField, "Text Alignment");
            if (textMPUiField != null) UnityEditor.Undo.RecordObject(textMPUiField, "Text Alignment");
            if (textField != null)
                textField.alignment = ToAnchor(alignment);
            if (textMPField != null)
                textMPField.alignment = alignment;
            if (textMPUiField != null)
                textMPUiField.alignment = alignment;
        }

        /// <summary>Выравнивание TMP → ближайший якорь <see cref="TextAnchor"/>.</summary>
        private static TextAnchor ToAnchor(TextAlignmentOptions alignment)
        {
            var horizontal = (HorizontalAlignmentOptions)((int)alignment & 0xFF);
            var vertical = (VerticalAlignmentOptions)((int)alignment & 0xFF00);

            var column = horizontal switch
            {
                HorizontalAlignmentOptions.Center or HorizontalAlignmentOptions.Geometry => 1,
                HorizontalAlignmentOptions.Right => 2,
                _ => 0,
            };
            var row = vertical switch
            {
                VerticalAlignmentOptions.Middle or VerticalAlignmentOptions.Geometry => 1,
                VerticalAlignmentOptions.Bottom or VerticalAlignmentOptions.Baseline => 2,
                _ => 0,
            };
            return (TextAnchor)(row * 3 + column);
        }

        /// <summary>Якорь <see cref="TextAnchor"/> → выравнивание TMP.</summary>
        private static TextAlignmentOptions FromAnchor(TextAnchor anchor)
        {
            var index = (int)anchor;
            var horizontal = (index % 3) switch
            {
                1 => HorizontalAlignmentOptions.Center,
                2 => HorizontalAlignmentOptions.Right,
                _ => HorizontalAlignmentOptions.Left,
            };
            var vertical = (index / 3) switch
            {
                1 => VerticalAlignmentOptions.Middle,
                2 => VerticalAlignmentOptions.Bottom,
                _ => VerticalAlignmentOptions.Top,
            };
            return (TextAlignmentOptions)((int)horizontal | (int)vertical);
        }
#endif

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