using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.EditorSettings
{
    [Serializable]
    public struct ThemeColors
    {
        public Color HeaderBg;
        public Color HeaderBgCollapsed;
        public Color BoxBg;
        public Color BorderColor;
        public Color BorderColorLight;
        public Color BadgeBg;
        public Color HeaderTextColor;
        public Color HeaderTextColorHover;
        public Color HeaderTextColorCollapsed;
        public Color TextColor;
        public Color TextColorInactive;
        public Color ErrorBg;
        public Color ErrorText;
        public Color ButtonBg;
        public Color ToggleBg;
        public Color SwitcherOnBg;
        public Color SwitcherOffBg;
        public Color ToggleBoxBorder;
        public Color DragIndicator;

        public Color GetColor(DefaultColors key)
        {
            switch (key)
            {
                case DefaultColors.HeaderBg: return HeaderBg;
                case DefaultColors.HeaderBgCollapsed: return HeaderBgCollapsed;
                case DefaultColors.BoxBg: return BoxBg;
                case DefaultColors.BorderColor: return BorderColor;
                case DefaultColors.BorderColorLight: return BorderColorLight;
                case DefaultColors.BadgeBg: return BadgeBg;
                case DefaultColors.HeaderTextColor: return HeaderTextColor;
                case DefaultColors.HeaderTextColorHover: return HeaderTextColorHover;
                case DefaultColors.HeaderTextColorCollapsed: return HeaderTextColorCollapsed;
                case DefaultColors.TextColor: return TextColor;
                case DefaultColors.TextColorInactive: return TextColorInactive;
                case DefaultColors.ErrorBg: return ErrorBg;
                case DefaultColors.ErrorText: return ErrorText;
                case DefaultColors.ButtonBg: return ButtonBg;
                case DefaultColors.ToggleBg: return ToggleBg;
                case DefaultColors.SwitcherOnBg: return SwitcherOnBg;
                case DefaultColors.SwitcherOffBg: return SwitcherOffBg;
                case DefaultColors.ToggleBoxBorder: return ToggleBoxBorder;
                case DefaultColors.DragIndicator: return DragIndicator;
                default: return Color.magenta;
            }
        }

        internal static ThemeColors CreateLight()
        {
            return new ThemeColors
            {
                HeaderBg = ParseColor("#C8C8C8ff"),
                HeaderBgCollapsed = ParseColor("#C8C8C8ff"),
                BoxBg = ParseColor("#D6D6D6ff"),
                BorderColor = ParseColor("#ABABABff"),
                BorderColorLight = ParseColor("#E9E9E9ff"),
                BadgeBg = ParseColor("#E9E9E9ff"),
                HeaderTextColor = ParseColor("#000000ff"),
                HeaderTextColorHover = ParseColor("#000000ff"),
                HeaderTextColorCollapsed = ParseColor("#000000ff"),
                TextColor = ParseColor("#000000ff"),
                TextColorInactive = ParseColor("#2C2C2Cff"),
                ErrorBg = ParseColor("#FA3333ff"),
                ErrorText = ParseColor("#ffffffff"),
                ButtonBg = ParseColor("#ffffffFF"),
                ToggleBg = ParseColor("#EFEFEFFF"),
                SwitcherOnBg = ParseColor("#00ff00ff"),
                SwitcherOffBg = ParseColor("#ff0000ff"),
                ToggleBoxBorder = ParseColor("#7F7F7Fff"),
                DragIndicator = ParseColor("#6F1717ff"),
            };
        }

        internal static ThemeColors CreatePro()
        {
            return new ThemeColors
            {
                HeaderBg = ParseColor("#383838ff"),
                HeaderBgCollapsed = ParseColor("#383838ff"),
                BoxBg = ParseColor("#575757ff"),
                BorderColor = ParseColor("#292929ff"),
                BorderColorLight = ParseColor("#575757ff"),
                BadgeBg = ParseColor("#303030ff"),
                HeaderTextColor = ParseColor("#d2d2d2ff"),
                HeaderTextColorHover = ParseColor("#d2d2d2ff"),
                HeaderTextColorCollapsed = ParseColor("#d2d2d2ff"),
                TextColor = ParseColor("#d2d2d2ff"),
                TextColorInactive = ParseColor("#a4a4a4ff"),
                ErrorBg = ParseColor("#FA3333ff"),
                ErrorText = ParseColor("#ffffffff"),
                ButtonBg = ParseColor("#ffffffff"),
                ToggleBg = ParseColor("#ffffffff"),
                SwitcherOnBg = ParseColor("#42FF42ff"),
                SwitcherOffBg = ParseColor("#ff4040ff"),
                ToggleBoxBorder = ParseColor("#292929ff"),
                DragIndicator = ParseColor("#9F2525ff"),
            };
        }

        private static Color ParseColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}