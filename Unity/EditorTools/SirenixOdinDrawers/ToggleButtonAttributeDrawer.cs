using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.EditorSettings;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="ToggleButtonAttribute"/>.
    /// Заменяет стандартное поле на горизонтальные кнопки-переключатели.
    /// Поддерживает bool, int, byte, enum.
    /// </summary>
    public sealed class ToggleButtonAttributeDrawer : OdinAttributeDrawer<ToggleButtonAttribute>
    {
        private static readonly Color ActiveShade = new(1.0f, 1.0f, 1.0f, 1f);
        private static readonly Color InactiveShade = new(0.85f, 0.85f, 0.85f, 1f);

        private ValueResolver<Dictionary<int, string>> _labelsResolver;
        private ValueResolver<Dictionary<int, Color>> _colorsResolver;

        protected override void Initialize()
        {
            if (!string.IsNullOrEmpty(Attribute.LabelsMethod))
                _labelsResolver = ValueResolver.Get<Dictionary<int, string>>(Property, Attribute.LabelsMethod);

            if (!string.IsNullOrEmpty(Attribute.ColorsMethod))
                _colorsResolver = ValueResolver.Get<Dictionary<int, Color>>(Property, Attribute.ColorsMethod);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var unityProp = Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath);
            if (unityProp == null || !IsSupportedType(unityProp))
            {
                SirenixEditorGUI.ErrorMessageBox(
                    $"{nameof(ToggleButtonAttribute)} поддерживает bool, int, byte и enum");
                CallNextDrawer(label);
                return;
            }

            var labels = ResolveLabels(unityProp);
            if (labels == null || labels.Count == 0)
            {
                SirenixEditorGUI.ErrorMessageBox("ToggleButton: для int/byte требуется labelsMethod");
                CallNextDrawer(label);
                return;
            }

            var colors = _colorsResolver?.GetValue();
            int currentValue = GetIntValue(unityProp);

            var totalRect = EditorGUILayout.GetControlRect();
            var buttonsRect = label != null ? EditorGUI.PrefixLabel(totalRect, label) : totalRect;

            var entries = labels.ToList();
            var numBtns = Attribute.IsSingleButton ? 1 : entries.Count;
            var gap = 3f;
            var buttonWidth = (buttonsRect.width - gap * (numBtns - 1)) / numBtns;

            var originalBg = GUI.backgroundColor;

            for (int i = 0; i < entries.Count; i++)
            {
                var kv = entries[i];
                bool isActive = currentValue == kv.Key;
                if (Attribute.IsSingleButton && !isActive)
                    continue;

                var btnRect = new Rect(
                    buttonsRect.x + i * (buttonWidth + gap),
                    buttonsRect.y,
                    buttonWidth,
                    buttonsRect.height);

                if (Attribute.IsSingleButton) btnRect = buttonsRect;

                Color baseColor = GetButtonColor(kv.Key, colors, entries.Count);
                GUI.backgroundColor = BlendColors(baseColor, isActive ? ActiveShade : InactiveShade);

                var style = numBtns == 1
                    ? EditorStyles.miniButton
                    : i == 0
                        ? EditorStyles.miniButtonLeft
                        : i == entries.Count - 1
                            ? EditorStyles.miniButtonRight
                            : EditorStyles.miniButtonMid;

                style.normal.textColor = isActive
                    ? ToolsSettings.GetLineColor(DefaultColors.TextColor)
                    : ToolsSettings.GetLineColor(DefaultColors.TextColorInactive);

                if (GUI.Button(btnRect, kv.Value, style))
                {
                    if (Attribute.IsSingleButton)
                        SetNextValue(unityProp, kv.Key, labels);
                    else
                        SetIntValue(unityProp, kv.Key);
                    unityProp.serializedObject.ApplyModifiedProperties();
                }

                if (Attribute.IsSingleButton)
                    break;
            }

            GUI.backgroundColor = originalBg;
        }

        // ════════════════════════════════════════════════════════
        //  Типы
        // ════════════════════════════════════════════════════════

        private static bool IsSupportedType(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Boolean
                   || property.propertyType == SerializedPropertyType.Integer
                   || property.propertyType == SerializedPropertyType.Enum;
        }

        private static int GetIntValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue ? 1 : 0;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Enum: return property.enumValueIndex;
                default: return 0;
            }
        }

        private static void SetIntValue(SerializedProperty property, int value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = value != 0; break;
                case SerializedPropertyType.Integer: property.intValue = value; break;
                case SerializedPropertyType.Enum: property.enumValueIndex = value; break;
            }
        }

        private static void SetNextValue(SerializedProperty property, int value, Dictionary<int, string> labels)
        {
            var keys = labels?.Keys.ToList();
            if (keys is { Count: > 0 })
            {
                var i = keys.IndexOf(value);
                if (++i >= keys.Count) i = 0;
                value = keys[i];
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = !property.boolValue; break;
                case SerializedPropertyType.Integer: property.intValue = value; break;
                case SerializedPropertyType.Enum: property.enumValueIndex = value; break;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Labels
        // ════════════════════════════════════════════════════════

        private Dictionary<int, string> ResolveLabels(SerializedProperty property)
        {
            if (_labelsResolver != null)
            {
                var resolved = _labelsResolver.GetValue();
                if (resolved != null) return resolved;
            }

            return GetDefaultLabels(property);
        }

        private static Dictionary<int, string> GetDefaultLabels(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return new Dictionary<int, string> { { 1, "On" }, { 0, "Off" } };

                case SerializedPropertyType.Enum:
                    var dict = new Dictionary<int, string>();
                    for (int i = 0; i < property.enumNames.Length; i++)
                        dict[i] = property.enumNames[i];
                    return dict;

                default:
                    return null;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Colors
        // ════════════════════════════════════════════════════════

        private static Color GetButtonColor(int key, Dictionary<int, Color> colors, int totalButtons)
        {
            if (colors != null && colors.TryGetValue(key, out var c))
                return c;

            if (totalButtons == 2)
                return key == 0
                    ? ToolsSettings.GetBgColor(DefaultColors.SwitcherOffBg)
                    : ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);

            return ToolsSettings.GetBgColor(DefaultColors.ToggleBg);
        }

        private static Color BlendColors(Color baseColor, Color multiplier)
        {
            return new Color(
                baseColor.r * multiplier.r,
                baseColor.g * multiplier.g,
                baseColor.b * multiplier.b,
                baseColor.a * multiplier.a);
        }
    }
}
