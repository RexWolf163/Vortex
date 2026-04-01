#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.EditorSettings;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.Elements
{
    public static class DrawingUtility
    {
        public const float RowHeight = 24f;
        public const float InnerHeight = 16f;
        public const float ElementSpacing = 2f;
        public const float ButtonWidth = 22f;
        public const float Padding = 4f;
        public const float OddCorrection = 0.95f;

        // ════════════════════════════════════════════════════════
        //  Кеш стилей
        // ════════════════════════════════════════════════════════

        private static GUIStyle _headerStyle;
        private static GUIStyle _headerCollapsedStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _nullLabelStyle;
        private static GUIStyle _dragHandleStyle;
        private static bool _lastIsProSkin;

        public static void CheckThemeChange()
        {
            if (_lastIsProSkin == EditorGUIUtility.isProSkin) return;
            _lastIsProSkin = EditorGUIUtility.isProSkin;
            _headerStyle = null;
            _headerCollapsedStyle = null;
            _badgeStyle = null;
            _nullLabelStyle = null;
            _dragHandleStyle = null;
        }

        public static GUIStyle GetHeaderStyle() =>
            _headerStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColor) },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 0, 0, 0),
                hover = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColorHover) },
                active = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColorHover) },
            };

        public static GUIStyle GetItemHeaderStyle() =>
            _headerCollapsedStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColorCollapsed) },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 0, 0, 0),
                hover = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColorHover) },
                active = { textColor = ToolsSettings.GetLineColor(DefaultColors.HeaderTextColorHover) },
            };

        public static GUIStyle GetBadgeStyle() =>
            _badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColor) },
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };

        public static GUIStyle GetDragHandleStyle() =>
            _dragHandleStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive) },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };

        // ════════════════════════════════════════════════════════
        //  Примитивы
        // ════════════════════════════════════════════════════════

        public static void DrawBoxBorder(Rect r, Color c, Color? c2 = null, bool raise = true, bool drawTop = true,
            bool drawBottom = true, bool drawLeft = true, bool drawRight = true)
        {
            c2 ??= c;
            if (drawTop) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), raise ? c2.Value : c);
            if (drawBottom) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), raise ? c : c2.Value);
            if (drawLeft) EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), raise ? c2.Value : c);
            if (drawRight) EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), raise ? c : c2.Value);
        }

        public static Color ApplyOddCorrection(Color color, int index)
        {
            if (index % 2 == 0)
                return new Color(color.r * OddCorrection, color.g * OddCorrection, color.b * OddCorrection, color.a);
            return color;
        }

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
            EditorGUI.HelpBox(boxRect, displayText, hasError ? MessageType.Error : msgType);

            if (hasError)
            {
                GUI.backgroundColor = originalColor;
            }
        }

        public static float CalcInfoBoxHeight(string displayText, float width)
        {
            var iconWidth = EditorGUIUtility.singleLineHeight * 2f;
            var charWidth = 6f;
            var textWidth = displayText.Length * charWidth;
            var numberStr = textWidth / (width - iconWidth);
            var textHeight = Mathf.Min(EditorGUIUtility.singleLineHeight * numberStr, 120f);
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

        public static void DrawSelector(Rect position, SerializedProperty property, string[] keys,
            object[] values = null, int currentIndex = 0, string placeholder = null)
        {
            values ??= keys as object[];
            // ── Popup ──
            placeholder ??= InspectorHandler.IsPropertyNullable(property) ? "——[NULL]——" : null;

            var current = InspectorHandler.GetPropertyValue(property).ToString();

            var old = GUI.backgroundColor;
            if (currentIndex < 0 || currentIndex >= keys.Length)
                GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ErrorBg);

            /*
            var wasOpen = GUI.Button(position,
                new GUIContent(GetDisplayText(currentIndex, placeholder, current)),
                EditorStyles.popup);
                */

            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
            var wasOpen = false;
            var evt = Event.current;

            if (evt.type == EventType.Repaint)
            {
                var text = placeholder ?? current;
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

            if (!wasOpen)
                return;

            var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(position.x, position.y));
            var screenRect = new Rect(screenPos.x, screenPos.y, position.width, position.height);
            SearchablePopupWindow.Show(screenRect, keys, placeholder, currentIndex, (newIndex) =>
            {
                if (newIndex >= 0 && newIndex < keys.Length)
                {
                    WriteValue(property, values[newIndex]);
                    property.serializedObject.ApplyModifiedProperties();
                }
            });
        }
        // ════════════════════════════════════════════════════════
        //  Запись выбранного значения
        // ════════════════════════════════════════════════════════

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

        /// <summary>
        /// Отрисовка дефолтного поля
        /// </summary>
        /// <param name="position"></param>
        /// <param name="data"></param>
        /// <param name="property"></param>
        public static void DrawDefaultField(Rect position, PropertyData data, SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = EditorGUI.TextField(position, property.stringValue);
                    break;

                case SerializedPropertyType.Integer:
                    property.intValue = EditorGUI.IntField(position, property.intValue);
                    break;

                case SerializedPropertyType.Float:
                    property.floatValue = EditorGUI.FloatField(position, property.floatValue);
                    break;

                case SerializedPropertyType.Boolean:
                    property.boolValue = EditorGUI.Toggle(position, property.boolValue);
                    break;

                case SerializedPropertyType.Enum:
                    var enumType = data.FieldInfo.FieldType;
                    var enumValue = Enum.ToObject(enumType, property.intValue);
                    var newValue = EditorGUI.EnumPopup(position, (Enum)enumValue);
                    property.intValue = Convert.ToInt32(newValue);
                    break;

                case SerializedPropertyType.Color:
                    property.colorValue = EditorGUI.ColorField(position, property.colorValue);
                    break;

                case SerializedPropertyType.Vector2:
                    property.vector2Value = EditorGUI.Vector2Field(position, GUIContent.none, property.vector2Value);
                    break;

                case SerializedPropertyType.Vector3:
                    property.vector3Value = EditorGUI.Vector3Field(position, GUIContent.none, property.vector3Value);
                    break;

                case SerializedPropertyType.Vector4:
                    property.vector4Value = EditorGUI.Vector4Field(position, GUIContent.none, property.vector4Value);
                    break;

                case SerializedPropertyType.Rect:
                    property.rectValue = EditorGUI.RectField(position, property.rectValue);
                    break;

                case SerializedPropertyType.Bounds:
                    property.boundsValue = EditorGUI.BoundsField(position, property.boundsValue);
                    break;

                case SerializedPropertyType.ObjectReference:
                    var objType = data.FieldInfo?.FieldType ?? typeof(Object);
                    try
                    {
                        EditorGUI.ObjectField(position, property, objType, GUIContent.none);
                    }
                    catch (Exception e)
                    {
                        //Ignore
                    }

                    break;

                case SerializedPropertyType.LayerMask:
                    property.intValue = EditorGUI.MaskField(position, property.intValue,
                        UnityEditorInternal.InternalEditorUtility.layers);
                    break;

                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = EditorGUI.CurveField(position, property.animationCurveValue);
                    break;

                case SerializedPropertyType.Gradient:
                    property.gradientValue = EditorGUI.GradientField(position, property.gradientValue);
                    break;

                case SerializedPropertyType.Character:
                    var charStr = EditorGUI.TextField(position, ((char)property.intValue).ToString());
                    if (!string.IsNullOrEmpty(charStr))
                        property.intValue = charStr[0];
                    break;

                case SerializedPropertyType.ManagedReference:
                    if (property.managedReferenceValue != null)
                    {
                        var typeName = property.managedReferenceValue.GetType().Name;
                        EditorGUI.LabelField(position, $"[SerializeReference] {typeName}");
                    }
                    else
                    {
                        EditorGUI.LabelField(position, "[SerializeReference] null");
                    }

                    break;

                default:
                    // Для сложных типов (struct, массивы, вложенные свойства)
                    // рисуем как обычное поле
                    if (property.hasVisibleChildren)
                    {
                        // Рекурсивная отрисовка дочерних элементов
                        var iterator = property.Copy();
                        var end = iterator.GetEndProperty();
                        var enterChildren = true;
                        var yPos = position.y;

                        while (iterator.NextVisible(enterChildren))
                        {
                            enterChildren = false;
                            if (SerializedProperty.EqualContents(iterator, end)) break;
                            var fieldHeight = EditorGUI.GetPropertyHeight(iterator, true);
                            var fieldRect = new Rect(position.x, yPos, position.width, fieldHeight);
                            EditorGUI.PropertyField(fieldRect, iterator, true);
                            yPos += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
                        }
                    }
                    else
                    {
                        // Fallback для неизвестных типов
                        EditorGUI.LabelField(position, property.ToString());
                    }

                    break;
            }
        }
    }
}
#endif