#if UNITY_EDITOR
using System;
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
    /// Работает для [SerializeField], [ShowInInspector] и параметров методов —
    /// тип определяется через <c>Property.Info.TypeOfValue</c>,
    /// чтение/запись идут через <c>ValueEntry.WeakSmartValue</c>.
    /// </summary>
    public sealed class ToggleButtonAttributeDrawer : OdinAttributeDrawer<ToggleButtonAttribute>
    {
        private static readonly Color ActiveShade = new(1.0f, 1.0f, 1.0f, 1f);
        private static readonly Color InactiveShade = new(0.85f, 0.85f, 0.85f, 1f);

        private ValueResolver<Dictionary<int, string>> _labelsResolver;
        private ValueResolver<Dictionary<int, Color>> _colorsResolver;

        // Кэш типа значения — определяется один раз
        private Type _valueType;
        private bool _isBool;
        private bool _isInt;
        private bool _isByte;
        private bool _isEnum;

        protected override void Initialize()
        {
            if (!string.IsNullOrEmpty(Attribute.LabelsMethod))
                _labelsResolver = ValueResolver.Get<Dictionary<int, string>>(Property, Attribute.LabelsMethod);

            if (!string.IsNullOrEmpty(Attribute.ColorsMethod))
                _colorsResolver = ValueResolver.Get<Dictionary<int, Color>>(Property, Attribute.ColorsMethod);

            _valueType = Property.Info.TypeOfValue;
            _isBool = _valueType == typeof(bool);
            _isInt = _valueType == typeof(int);
            _isByte = _valueType == typeof(byte);
            _isEnum = _valueType != null && _valueType.IsEnum;
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (!IsSupportedType())
            {
                SirenixEditorGUI.ErrorMessageBox(
                    $"{nameof(ToggleButtonAttribute)} поддерживает bool, int, byte и enum");
                CallNextDrawer(label);
                return;
            }

            var labels = ResolveLabels();
            if (labels == null || labels.Count == 0)
            {
                SirenixEditorGUI.ErrorMessageBox("ToggleButton: для int/byte требуется labelsMethod");
                CallNextDrawer(label);
                return;
            }

            var colors = _colorsResolver?.GetValue();
            int currentValue = GetIntValue();

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
                        SetNextValue(kv.Key, labels);
                    else
                        SetIntValue(kv.Key);
                }

                if (Attribute.IsSingleButton)
                    break;
            }

            GUI.backgroundColor = originalBg;
        }

        // ════════════════════════════════════════════════════════
        //  Типы
        // ════════════════════════════════════════════════════════

        private bool IsSupportedType() => _isBool || _isInt || _isByte || _isEnum;

        /// <summary>
        /// Читает текущее значение как int.
        /// Для enum возвращает порядковый индекс в Enum.GetValues (совместимо с enumValueIndex),
        /// чтобы пользовательские labelsMethod-словари с ключами 0,1,2,... продолжали работать.
        /// </summary>
        private int GetIntValue()
        {
            var raw = ValueEntry.WeakSmartValue;
            if (raw == null) return 0;

            if (_isBool) return ((bool)raw) ? 1 : 0;
            if (_isInt) return (int)raw;
            if (_isByte) return (byte)raw;

            if (_isEnum)
            {
                var values = Enum.GetValues(_valueType);
                var rawAsLong = Convert.ToInt64(raw);
                for (int i = 0; i < values.Length; i++)
                {
                    if (Convert.ToInt64(values.GetValue(i)) == rawAsLong)
                        return i;
                }
                return 0;
            }

            return 0;
        }

        /// <summary>
        /// Записывает значение по int. Для enum value — порядковый индекс.
        /// </summary>
        private void SetIntValue(int value)
        {
            if (_isBool)
            {
                ValueEntry.WeakSmartValue = value != 0;
                return;
            }

            if (_isInt)
            {
                ValueEntry.WeakSmartValue = value;
                return;
            }

            if (_isByte)
            {
                ValueEntry.WeakSmartValue = (byte)value;
                return;
            }

            if (_isEnum)
            {
                var values = Enum.GetValues(_valueType);
                if (value >= 0 && value < values.Length)
                    ValueEntry.WeakSmartValue = values.GetValue(value);
            }
        }

        private void SetNextValue(int currentValue, Dictionary<int, string> labels)
        {
            // Для bool single-button — простая инверсия, как в оригинале
            if (_isBool)
            {
                var raw = ValueEntry.WeakSmartValue;
                ValueEntry.WeakSmartValue = !(raw is bool b && b);
                return;
            }

            // Для остальных типов — следующий ключ в labels
            int next = currentValue;
            var keys = labels?.Keys.ToList();
            if (keys is { Count: > 0 })
            {
                var i = keys.IndexOf(currentValue);
                if (++i >= keys.Count) i = 0;
                next = keys[i];
            }

            SetIntValue(next);
        }

        // ════════════════════════════════════════════════════════
        //  Labels
        // ════════════════════════════════════════════════════════

        private Dictionary<int, string> ResolveLabels()
        {
            if (_labelsResolver != null)
            {
                var resolved = _labelsResolver.GetValue();
                if (resolved != null) return resolved;
            }

            return GetDefaultLabels();
        }

        private Dictionary<int, string> GetDefaultLabels()
        {
            if (_isBool)
                return new Dictionary<int, string> { { 1, "On" }, { 0, "Off" } };

            if (_isEnum)
            {
                var dict = new Dictionary<int, string>();
                var names = Enum.GetNames(_valueType);
                for (int i = 0; i < names.Length; i++)
                    dict[i] = names[i];
                return dict;
            }

            return null;
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
#endif
