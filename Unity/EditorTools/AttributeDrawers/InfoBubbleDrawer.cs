#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Bus;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(InfoBubbleAttribute))]
    public class InfoBubbleDrawer : MultiDrawer
    {
        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            var position = data.Position;
            var property = data.Property;
            var infoBoxAttribute = (InfoBubbleAttribute)attribute;

            var width = data.Width;
            if (width == 0)
                width = data.Position.width;

            bool? shouldHide = false;
            if (!string.IsNullOrEmpty(infoBoxAttribute.HideIf))
            {
                shouldHide = ReflectionHelper.InvokeBoolMethod(property, infoBoxAttribute.HideIf);
                if (shouldHide == true)
                    return 0;
            }

            if (shouldHide == null)
            {
                var text = $"Method {infoBoxAttribute.HideIf} not Found";
                var h = DrawingUtility.CalcInfoBoxHeight(text, width);

                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(position, text, true, infoBoxAttribute.Icon);

                return h;
            }

            var (displayText, isError) = ResolveDisplayText(infoBoxAttribute, property);

            var textHeight = string.IsNullOrEmpty(displayText)
                ? 0
                : DrawingUtility.CalcInfoBoxHeight(displayText, width);
            if (!string.IsNullOrEmpty(displayText) && !onlyCalculation)
                DrawingUtility.MakeInfoBox(position, displayText, isError, infoBoxAttribute.Icon);

            return textHeight;
        }

        private static (string text, bool isError) ResolveDisplayText(InfoBubbleAttribute attr,
            SerializedProperty property)
        {
            return ReflectionHelper.ResolveTextOrMethod(property, attr.TextOrMethod);
        }
    }
}
#endif