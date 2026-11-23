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

        [Header("Graph editors")] public Color GraphBg;
        public Color GraphNodeBg;
        public Color GraphNodeCurrentBg;
        public Color GraphNodeText;
        public Color GraphNodeTagText;
        public Color GraphNodeBorder;
        public Color GraphStart;
        public Color GraphSelected;
        public Color GraphWarning;
        public Color GraphError;
        public Color GraphEdge;
        public Color GraphEdgeNext;
        public Color GraphLabelBg;
        public Color GraphPort;

        [Header("Tool statuses")] public Color StatusOk;
        public Color StatusWarning;
        public Color StatusError;
        public Color StatusInfo;
        public Color StatusAccent;

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
                case DefaultColors.GraphBg: return GraphBg;
                case DefaultColors.GraphNodeBg: return GraphNodeBg;
                case DefaultColors.GraphNodeCurrentBg: return GraphNodeCurrentBg;
                case DefaultColors.GraphNodeText: return GraphNodeText;
                case DefaultColors.GraphNodeTagText: return GraphNodeTagText;
                case DefaultColors.GraphNodeBorder: return GraphNodeBorder;
                case DefaultColors.GraphStart: return GraphStart;
                case DefaultColors.GraphSelected: return GraphSelected;
                case DefaultColors.GraphWarning: return GraphWarning;
                case DefaultColors.GraphError: return GraphError;
                case DefaultColors.GraphEdge: return GraphEdge;
                case DefaultColors.GraphEdgeNext: return GraphEdgeNext;
                case DefaultColors.GraphLabelBg: return GraphLabelBg;
                case DefaultColors.GraphPort: return GraphPort;
                case DefaultColors.StatusOk: return StatusOk;
                case DefaultColors.StatusWarning: return StatusWarning;
                case DefaultColors.StatusError: return StatusError;
                case DefaultColors.StatusInfo: return StatusInfo;
                case DefaultColors.StatusAccent: return StatusAccent;
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
                GraphBg = ParseColor("#A9A9A9ff"),
                GraphNodeBg = ParseColor("#F4F4F4ff"),
                GraphNodeCurrentBg = ParseColor("#B6E3B6ff"),
                GraphNodeText = ParseColor("#111111ff"),
                GraphNodeTagText = ParseColor("#555555ff"),
                GraphNodeBorder = ParseColor("#6E6E6Eff"),
                GraphStart = ParseColor("#1565C0ff"),
                GraphSelected = ParseColor("#8E24AAff"),
                GraphWarning = ParseColor("#D86F00ff"),
                GraphError = ParseColor("#D32F2Fff"),
                GraphEdge = ParseColor("#333333ff"),
                GraphEdgeNext = ParseColor("#00838Fff"),
                GraphLabelBg = ParseColor("#F4F4F4ff"),
                GraphPort = ParseColor("#6E6E6Eff"),
                StatusOk = ParseColor("#11661Fff"),
                StatusWarning = ParseColor("#8A4F00ff"),
                StatusError = ParseColor("#B71C1Cff"),
                StatusInfo = ParseColor("#0D57A8ff"),
                StatusAccent = ParseColor("#8E24AAff"),
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
                GraphBg = ParseColor("#1E1E1Eff"),
                GraphNodeBg = ParseColor("#3A3A3Aff"),
                GraphNodeCurrentBg = ParseColor("#2E5E33ff"),
                GraphNodeText = ParseColor("#E6E6E6ff"),
                GraphNodeTagText = ParseColor("#A0A0A0ff"),
                GraphNodeBorder = ParseColor("#6A6A6Aff"),
                GraphStart = ParseColor("#4DA3FFff"),
                GraphSelected = ParseColor("#C792EAff"),
                GraphWarning = ParseColor("#FFA726ff"),
                GraphError = ParseColor("#FF5252ff"),
                GraphEdge = ParseColor("#BDBDBDff"),
                GraphEdgeNext = ParseColor("#4DD0E1ff"),
                GraphLabelBg = ParseColor("#2A2A2Aff"),
                GraphPort = ParseColor("#9E9E9Eff"),
                StatusOk = ParseColor("#4DD959ff"),
                StatusWarning = ParseColor("#F2BF40ff"),
                StatusError = ParseColor("#F25959ff"),
                StatusInfo = ParseColor("#73B8FFff"),
                StatusAccent = ParseColor("#F28CF2ff"),
            };
        }

        private static Color ParseColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}