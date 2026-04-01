using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    public sealed class TimerAttributeDrawer : OdinAttributeDrawer<TimerDrawAttribute, long>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            GUI.enabled = false;
            SirenixEditorFields.TextField(label, ConvertToTime(ValueEntry.SmartValue));
            GUI.enabled = true;
        }

        private string ConvertToTime(long timeSpan)
        {
            var time = TimeSpan.FromSeconds(timeSpan);
            if (time.Days > 0)
                return $"{time:d\\d\\ hh\\:mm\\:ss}";
            return $"{time:hh\\:mm\\:ss}";
        }
    }

    public sealed class DateTimerAttributeDrawer : OdinAttributeDrawer<DateTimerDrawAttribute, long>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            GUI.enabled = false;
            SirenixEditorFields.TextField(label, ConvertToTime(ValueEntry.SmartValue));
            GUI.enabled = true;
        }

        private string ConvertToTime(long timeSpan)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timeSpan).DateTime;
            return dateTime.ToString("dd.MM.yyyy HH:mm:ss");
        }
    }
}