#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(MakeButtonAttribute))]
    public class MakeButtonAttributeDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute) => data.HideLabel();

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var attr = attribute as MakeButtonAttribute;
            var buttonsRect = data.Position;
            var baseColor = ToolsSettings.GetBgColor(DefaultColors.ToggleBg);
            GUI.backgroundColor = baseColor;
            var style = EditorStyles.miniButton;
            style.normal.textColor = ToolsSettings.GetLineColor(DefaultColors.TextColor);

            var methodName = attr.MethodName;
            var method = ReflectionHelper.FindMethod(data.Owner, methodName);
            var label = method == null ? methodName : method.Invoke(null, null) as string;

            if (GUI.Button(buttonsRect, label, style))
            {
            }
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            //TODO пропсиать обработку ошибок
            return 0;
        }
    }
}
#endif