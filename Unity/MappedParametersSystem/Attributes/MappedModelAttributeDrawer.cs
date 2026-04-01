#if UNITY_EDITOR
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.MappedParametersSystem.Base.Preset;

namespace Vortex.Unity.MappedParametersSystem.Attributes
{
    public class MappedModelAttributeDrawer : OdinAttributeDrawer<MappedModelAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var error = ValueEntry.SmartValue.IsNullOrWhitespace();

            var btnWidth = error || ValueEntry.SmartValue.IsNullOrWhitespace() ? 0f : 40f;
            var controlRect =
                EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);

            EditorGUI.BeginChangeCheck();
            var color = GUI.color;
            if (error)
                GUI.color = Color.red;

            var dropdownRect = new Rect(
                controlRect.x,
                controlRect.y,
                controlRect.width - btnWidth,
                controlRect.height
            );
            var buttonRect = new Rect(
                dropdownRect.x + dropdownRect.width,
                controlRect.y,
                btnWidth,
                controlRect.height
            );

            var resource = Resources.LoadAll<ParametersMapStorage>("");
            if (resource == null || resource.Length == 0)
                return;

            //TODO переделать под сменный драйвер
            var list = resource.Select(p => p.guid);
            ValueEntry.SmartValue = OdinDropdownTool.DropdownSelector(dropdownRect, label, ValueEntry.SmartValue, list);
            if (EditorGUI.EndChangeCheck())
                GUI.color = color;

            if (!ValueEntry.SmartValue.IsNullOrWhitespace() && GUI.Button(buttonRect, "Find"))
                FindRecordAsset(ValueEntry.SmartValue);
        }

        private void FindRecordAsset(string recordId)
        {
            /*
            var driver = MappedParameters.GetDriver() as IDriverEditor;
            if (driver == null)
            {
                Debug.LogError("[DbRecordAttributeDrawer] Не получилось получить драйвер Базы данных.");
                return;
            }

            var resource = driver.GetPresetForRecord(recordId) as Object;
            if (resource == null)
            {
                Debug.LogWarning("[DbRecordAttributeDrawer] Пресет не найден");
                return;
            }
            */

            //TODO переделать под сменный драйвер

            var resource = Resources.LoadAll<ParametersMapStorage>("");
            if (resource == null || resource.Length == 0)
                return;
            var res = resource.FirstOrDefault(p => p.guid == ValueEntry.SmartValue);
            Selection.activeObject = res;
        }
    }
}

#endif