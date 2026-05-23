#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.Extensions.Editor;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.Elements
{
    public static class DrawingUtility
    {
        // ════════════════════════════════════════════════════════
        //  Box border
        // ════════════════════════════════════════════════════════

        public static void DrawBoxBorder(Rect r, Color c, Color? c2 = null, bool raise = true,
            bool drawTop = true, bool drawBottom = true, bool drawLeft = true, bool drawRight = true)
        {
            c2 ??= c;
            if (drawTop) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), raise ? c2.Value : c);
            if (drawBottom) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), raise ? c : c2.Value);
            if (drawLeft) EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), raise ? c2.Value : c);
            if (drawRight) EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), raise ? c : c2.Value);
        }

        // ════════════════════════════════════════════════════════
        //  InfoBox
        // ════════════════════════════════════════════════════════

        public static void MakeInfoBox(Rect position, string displayText, bool hasError,
            InfoMessageType icon = InfoMessageType.Info)
        {
            var msgType = ToMessageType(icon);
            var originalColor = GUI.backgroundColor;
            if (hasError)
                GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ErrorBg);

            var textHeight = Mathf.Max(CalcInfoBoxHeight(displayText, position.width),
                EditorGUIUtility.singleLineHeight * 2f);
            textHeight = Mathf.Min(textHeight, 120f);

            var boxRect = new Rect(position.x, position.y, position.width, textHeight);
            RichTextHelpBox.Create(boxRect, displayText, hasError ? MessageType.Error : msgType);

            if (hasError)
                GUI.backgroundColor = originalColor;
        }

        public static float CalcInfoBoxHeight(string displayText, float width)
        {
            const float charWidth = 6f;
            var iconWidth = EditorGUIUtility.singleLineHeight * 2f;
            var textWidth = displayText.Length * charWidth;
            var lines = textWidth / (width - iconWidth);
            var textHeight = Mathf.Min(EditorGUIUtility.singleLineHeight * lines, 120f);
            textHeight = Mathf.Max(textHeight, EditorGUIUtility.singleLineHeight * 2f);
            return textHeight;
        }

        private static MessageType ToMessageType(InfoMessageType type)
        {
            switch (type)
            {
                case InfoMessageType.Info: return MessageType.Info;
                case InfoMessageType.Warning: return MessageType.Warning;
                case InfoMessageType.Error: return MessageType.Error;
                default: return MessageType.None;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Selector (popup with search)
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Popup-селектор с поиском, привязанный к <see cref="SerializedProperty"/>.
        /// Используется когда значение хранится в сериализованном поле Unity.
        /// </summary>
        public static void DrawSelector(Rect position, SerializedProperty property, string[] keys,
            object[] values = null, int currentIndex = 0, string placeholder = null)
        {
            values ??= keys as object[];
            placeholder ??= InspectorHandler.IsPropertyNullable(property) ? "——[NULL]——" : null;
            var current = InspectorHandler.GetPropertyValue(property)?.ToString();

            DrawSelectorCore(position, current, keys, placeholder, currentIndex, newIndex =>
            {
                WriteValue(property, values[newIndex]);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        /// <summary>
        /// Popup-селектор с поиском без зависимости от <see cref="SerializedProperty"/>.
        /// Подходит для [ShowInInspector]-полей, параметров методов и любых non-SP контекстов —
        /// чтение текущего значения и запись нового делегируются вызывающей стороне.
        /// </summary>
        public static void DrawSelector(Rect position, string currentValue, string[] keys,
            Action<string> onSelect, int currentIndex = 0, string placeholder = null)
        {
            placeholder ??= "——[NULL]——";
            DrawSelectorCore(position, currentValue, keys, placeholder, currentIndex,
                newIndex => onSelect?.Invoke(keys[newIndex]));
        }

        /// <summary>
        /// Общий рендер popup'а: отрисовка текущего состояния + открытие <see cref="SearchablePopupWindow"/>
        /// при клике. Запись значения делегируется через <paramref name="onIndexSelected"/>.
        /// </summary>
        private static void DrawSelectorCore(Rect position, string currentText, string[] keys,
            string placeholder, int currentIndex, Action<int> onIndexSelected)
        {
            var old = GUI.backgroundColor;
            if (currentIndex < 0 || currentIndex >= keys.Length)
                GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ErrorBg);

            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
            var wasOpen = false;
            var evt = Event.current;

            if (evt.type == EventType.Repaint)
            {
                var text = placeholder ?? currentText;
                if (currentIndex >= 0 && currentIndex < keys.Length)
                    text = keys[currentIndex];
                EditorStyles.popup.Draw(position, new GUIContent(text), controlId, false);
            }
            else if (evt.type == EventType.MouseDown && position.Contains(evt.mousePosition))
            {
                wasOpen = true;
                evt.Use();
            }

            GUI.backgroundColor = old;

            if (!wasOpen) return;

            var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(position.x, position.y));
            var screenRect = new Rect(screenPos.x, screenPos.y, position.width, position.height);
            SearchablePopupWindow.Show(screenRect, keys, placeholder, currentIndex, newIndex =>
            {
                if (newIndex >= 0 && newIndex < keys.Length)
                    onIndexSelected?.Invoke(newIndex);
            });
        }

        private static void WriteValue(SerializedProperty property, object value)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value as string;
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (value is int iv) property.intValue = iv;
                    else if (value is long lv) property.intValue = (int)lv;
                    else if (int.TryParse(value?.ToString(), out var pi)) property.intValue = pi;
                    break;

                case SerializedPropertyType.Float:
                    if (value is float fv) property.floatValue = fv;
                    else if (value is double dv) property.floatValue = (float)dv;
                    else if (float.TryParse(value?.ToString(), out var pf)) property.floatValue = pf;
                    break;

                case SerializedPropertyType.Boolean:
                    if (value is bool bv) property.boolValue = bv;
                    break;

                case SerializedPropertyType.Enum:
                    if (value is int ei) property.enumValueIndex = ei;
                    else if (value is Enum ev) property.enumValueIndex = Convert.ToInt32(ev);
                    else
                    {
                        var idx = Array.IndexOf(property.enumNames, value?.ToString());
                        if (idx >= 0) property.enumValueIndex = idx;
                    }
                    break;

                case SerializedPropertyType.ObjectReference:
                    if (value is Object uo) property.objectReferenceValue = uo;
                    break;
            }
        }
    }
}
#endif
