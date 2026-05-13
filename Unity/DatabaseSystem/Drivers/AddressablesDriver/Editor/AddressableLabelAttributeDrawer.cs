#if UNITY_EDITOR && ENABLE_ADDRESSABLES
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver.Extensions;

namespace Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver.Editor
{
    public class AddressableLabelAttributeDrawer : OdinAttributeDrawer<AddressableLabelAttribute, string>
    {
        private const string Empty = "[NONE]";

        private static readonly Color MissingColor = new(1f, 0.55f, 0.55f);

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var options = GetLabels();
            options.Insert(0, Empty);
            var current = ValueEntry.SmartValue ?? string.Empty;
            var index = options.IndexOf(current);
            if (index < 0) index = 0; // не нашли — показываем string.Empty

            // Подсветка пустого значения: визуальный сигнал «label не выбран».
            var isMissing = index == 0;
            var prevColor = GUI.backgroundColor;
            if (isMissing) GUI.backgroundColor = MissingColor;

            try
            {
                EditorGUI.BeginChangeCheck();
                var newIndex = label != null
                    ? EditorGUILayout.Popup(label, index, options.ToArray())
                    : EditorGUILayout.Popup(index, options.ToArray());
                if (!EditorGUI.EndChangeCheck()) return;

                ValueEntry.SmartValue = newIndex == 0 ? string.Empty : options[newIndex];
            }
            finally
            {
                if (isMissing) GUI.backgroundColor = prevColor;
            }
        }

        private List<string> GetLabels()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return new List<string>();

            var labels = new HashSet<string>();
            foreach (var group in settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    if (entry.labels == null)
                        continue;
                    foreach (var label in entry.labels)
                    {
                        if (string.IsNullOrEmpty(label))
                            continue;
                        labels.Add(label);
                    }
                }
            }

            return labels.OrderBy(l => l).ToList();
        }
    }
}
#endif