#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(ToggleButtonAttribute))]
    public class ToggleButtonDrawer : MultiDrawer
    {
        private static readonly Color ActiveShade = new(1.0f, 1.0f, 1.0f, 1f);
        private static readonly Color InactiveShade = new(0.85f, 0.85f, 0.85f, 1f);

        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
        }
        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;

            var attr = (ToggleButtonAttribute)attribute;

            if (!IsSupportedType(property))
                return;

            var labels = ResolveLabels(property, attr);
            if (labels == null || labels.Count == 0)
                return;

            var colors = ResolveColors(property, attr);
            int currentValue = GetIntValue(property);

            // Buttons
            var buttonsRect = data.Position;

            var entries = labels.ToList();
            var numBtns = attr.IsSingleButton ? 1 : entries.Count;
            var gap = 3f;
            var buttonWidth = (buttonsRect.width - gap * (numBtns - 1)) / numBtns;

            var originalBg = GUI.backgroundColor;

            for (int i = 0; i < entries.Count; i++)
            {
                var kv = entries[i];
                bool isActive = currentValue == kv.Key;
                if (attr.IsSingleButton && !isActive)
                    continue;

                var btnRect = new Rect(
                    buttonsRect.x + i * (buttonWidth + gap),
                    buttonsRect.y,
                    buttonWidth,
                    buttonsRect.height);

                if (attr.IsSingleButton) btnRect = buttonsRect;


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
                //style.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;

                if (GUI.Button(btnRect, kv.Value, style))
                {
                    if (attr.IsSingleButton)
                        SetNextValue(property, kv.Key, labels);
                    else
                        SetIntValue(property, kv.Key);
                    property.serializedObject.ApplyModifiedProperties();
                    //ReflectionHelper.InvokeOnValueChanged(property);
                }

                if (attr.IsSingleButton)
                    break;
            }

            GUI.backgroundColor = originalBg;
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            if (!IsSupportedType(data.Property))
            {
                var text = $"{nameof(ToggleButtonAttribute)} поддерживает bool, int, byte и enum";
                var h = DrawingUtility.CalcInfoBoxHeight(text, data.Position.width);
                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(data.Position, text, true, InfoMessageType.Error);
                return h + 2f;
            }

            var labels = ResolveLabels(data.Property, attribute as ToggleButtonAttribute);
            if (labels == null || labels.Count == 0)
            {
                var text = "ToggleButton: для int/byte требуется labelsMethod";
                var h = DrawingUtility.CalcInfoBoxHeight(text, data.Position.width);
                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(data.Position, text, true, InfoMessageType.Error);
                return h + 2f;
            }

            return 0;
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
                case SerializedPropertyType.Boolean:
                    property.boolValue = value != 0;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = value;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = value;
                    break;
            }
        }

        private static void SetNextValue(SerializedProperty property, int value,
            Dictionary<int, string> labels)
        {
            var keys = labels?.Keys.ToList();
            if (keys is { Count: > 0 })
            {
                var i = keys.IndexOf(value);
                if (++i >= keys.Count)
                    i = 0;
                value = keys[i];
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = !property.boolValue;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = value;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = value;
                    break;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Labels
        // ════════════════════════════════════════════════════════

        private static Dictionary<int, string> ResolveLabels(SerializedProperty property, ToggleButtonAttribute attr)
        {
            if (attr != null && !string.IsNullOrEmpty(attr.LabelsMethod))
            {
                var result = ReflectionHelper.InvokeMethod<Dictionary<int, string>>(property, attr.LabelsMethod);
                if (result != null) return result;
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
                    // int/byte без labelsMethod — невозможно определить набор кнопок
                    return null;
            }
        }

        // ════════════════════════════════════════════════════════
        //  Colors
        // ════════════════════════════════════════════════════════

        private static Dictionary<int, Color> ResolveColors(SerializedProperty property, ToggleButtonAttribute attr)
        {
            if (!string.IsNullOrEmpty(attr.ColorsMethod))
                return ReflectionHelper.InvokeMethod<Dictionary<int, Color>>(property, attr.ColorsMethod);
            return null;
        }

        private static Color GetButtonColor(int key, Dictionary<int, Color> colors, int totalButtons)
        {
            if (colors != null && colors.TryGetValue(key, out var c))
                return c;

            // Defaults: для двух кнопок — Off(red)/On(green), иначе — палитра по ToggleOnBg
            if (totalButtons == 2)
                return key == 0
                    ? ToolsSettings.GetBgColor(DefaultColors.SwitcherOffBg)
                    : ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);

            return ToolsSettings.GetBgColor(DefaultColors.ToggleBg);
        }

        // ════════════════════════════════════════════════════════
        //  Утилиты
        // ════════════════════════════════════════════════════════

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
#endif