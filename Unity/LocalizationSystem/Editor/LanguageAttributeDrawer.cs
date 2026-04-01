#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.LocalizationSystem.Editor
{
    [CustomPropertyDrawer(typeof(LanguageAttribute))]
    public class LanguageAttributeDrawer : MultiDrawer
    {
        private bool _hasError = false;

        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            if (_hasError)
                return;
            var list = Localization.GetLanguages();

            var val = data.Property.stringValue;
            var currentIndex = val.IsNullOrWhitespace() ? -1 : list.IndexOf(val);
            DrawingUtility.DrawSelector(data.Position, data.Property, list.ToArray(), currentIndex: currentIndex);
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            if (data.Property.propertyType != SerializedPropertyType.String)
            {
                _hasError = true;
                var text = "Only string is supported for Language Attribute";
                var h = DrawingUtility.CalcInfoBoxHeight(text, data.Position.width);
                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(data.Position, text, true);
                return h;
            }

            return 0;
        }
    }
}
#endif