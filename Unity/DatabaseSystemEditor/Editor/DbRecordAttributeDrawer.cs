#if UNITY_EDITOR
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.Extensions.Editor;
using Object = UnityEngine.Object;

namespace Vortex.Unity.DatabaseSystemEditor.Editor
{
    public class DbRecordAttributeDrawer : OdinAttributeDrawer<DbRecordAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var driver = Database.GetDriver() as IDriverEditor;
            if (driver == null)
            {
                Debug.LogError("[DbRecordAttributeDrawer] Не получилось получить драйвер Базы данных.");
                return;
            }

            driver.ReloadDatabase();

            var btnWidth = ValueEntry.SmartValue.IsNullOrWhitespace() ? 0f : 40f;
            var controlRect =
                EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);

            EditorGUI.BeginChangeCheck();
            var color = GUI.color;
            var test = TestMethod();

            if (!test)
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

            var ddList = new ValueDropdownList<string>();

            if (Attribute.RecordType == null || Attribute.RecordType == RecordTypes.Singleton)
            {
                var list = Attribute.RecordClass != null
                    ? Database.GetRecords(Attribute.RecordClass)
                    : Database.GetRecords();
                foreach (var record in list)
                    ddList.Add(record.Name.Replace(".", "/"), record.GuidPreset);
            }

            if (Attribute.RecordType == null || Attribute.RecordType == RecordTypes.MultiInstance)
            {
                var list = Database.GetMultiInstancePresets();

                foreach (var guid in list)
                {
                    var record = driver.GetPresetForRecord(guid) as IRecordPreset;
                    if (record == null)
                    {
                        Debug.LogError(
                            $"[DbRecordAttributeDrawer] Ошибка приведения элемента GUID#{guid} к типу IRecordPreset.");
                        return;
                    }

                    if (Attribute.RecordClass != null &&
                        !Attribute.RecordClass.IsAssignableFrom(record.GetData().GetType()))
                        continue;

                    var name = record.Name.Replace(".", "/");
                    ddList.Add(name, record.GuidPreset);
                }

                ValueEntry.SmartValue =
                    OdinDropdownTool.DropdownSelector(dropdownRect, label, ValueEntry.SmartValue, ddList);
            }

            if (EditorGUI.EndChangeCheck())
                TestMethod();

            GUI.color = color;

            if (!ValueEntry.SmartValue.IsNullOrWhitespace() && GUI.Button(buttonRect, "Find"))
                FindRecordAsset(ValueEntry.SmartValue);
        }

        private bool TestMethod() =>
            !ValueEntry.SmartValue.IsNullOrWhitespace() && Database.TestRecord(ValueEntry.SmartValue);

        private void FindRecordAsset(string recordId)
        {
            var driver = Database.GetDriver() as IDriverEditor;
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

            Selection.activeObject = resource;
        }
    }
}
#endif