using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    public sealed class TimeAttributeDrawer : OdinAttributeDrawer<TimeDrawAttribute, long>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();

            GUIHelper.PushLabelWidth(100);
            ValueEntry.SmartValue = Math.Max(0,
                SirenixEditorFields.LongField(rect.AlignLeft(rect.width * 0.6f), label, ValueEntry.SmartValue));
            GUI.enabled = false;
            SirenixEditorFields.TextField(rect.AlignRight(rect.width * 0.4f), ConvertToTime(ValueEntry.SmartValue));
            GUI.enabled = true;
            GUIHelper.PopLabelWidth();
        }

        private string ConvertToTime(long timeSpan)
        {
            var time = TimeSpan.FromSeconds(timeSpan);
            if (time.Days > 0)
                return $"{time:d\\d\\ hh\\:mm\\:ss}";
            return $"{time:hh\\:mm\\:ss}";
        }
    }
}