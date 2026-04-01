#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.AudioSystem.Editor
{
    [CustomPropertyDrawer(typeof(AudioChannelNameAttribute))]
    public class AudioChannelNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                var text = "Only string is supported for AudioChannel Attribute";
                var h = DrawingUtility.CalcInfoBoxHeight(text, position.width);
                DrawingUtility.MakeInfoBox(position, text, true);
                return;
            }

            var list = AudioController.GetChannelsList();
            var val = property.stringValue;
            property.stringValue = OdinDropdownTool.DropdownSelector(position, label, val, list.ToArray());
        }

        /*
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            if (data.Property.propertyType != SerializedPropertyType.String)
                return;
            var list = AudioController.Settings.Channels.Keys.ToList();
            var val = data.Property.stringValue;
            var currentIndex = val.IsNullOrWhitespace() ? -1 : list.IndexOf(val);
            OdinDropdownTool.DropdownSelector(data.Position, data.Label, list[currentIndex], list.ToArray());
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            if (data.Property.propertyType != SerializedPropertyType.String)
            {
                var text = "Only string is supported for AudioChannel Attribute";
                var h = DrawingUtility.CalcInfoBoxHeight(text, data.Position.width);
                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(data.Position, text, true);
                return h;
            }

            return 0;
        }
    */
    }
}
#endif