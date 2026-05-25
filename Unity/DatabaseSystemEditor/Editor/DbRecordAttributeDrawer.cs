#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.EditorTools.Elements;
using Object = UnityEngine.Object;

namespace Vortex.Unity.DatabaseSystemEditor.Editor
{
    /// <summary>
    /// Drawer для <see cref="DbRecordAttribute"/>. Рисует список записей <see cref="Database"/>
    /// в виде древовидного дропдауна с поиском по полному пути (см. <see cref="SearchablePopup"/>).
    ///
    /// Поведение:
    ///  • Имена записей с разделителем "." преобразуются в иерархию через "/" — Odin/SearchablePopup
    ///    строит из них раскрываемые группы (например, <c>"February.miss.en.1"</c> → разделы
    ///    <c>February → miss → en → 1</c>).
    ///  • Поиск матчит подстрокой по полному пути, поэтому ввод имени раздела находит весь
    ///    его поддерев, а в выдаче поиска родительские группы остаются видимыми — лист
    ///    показывается в контексте своих групп.
    ///  • Атрибут поддерживает фильтрацию по <see cref="DbRecordAttribute.RecordType"/>
    ///    (Singleton / MultiInstance / null = обе ветки) и по <see cref="DbRecordAttribute.RecordClass"/>.
    ///  • Слева от дропдауна — индикатор валидности GUID: красный, если запись с таким GUID
    ///    отсутствует в Database. Справа — кнопка <c>Find</c>, выделяющая ассет пресета в Project.
    /// </summary>
    public class DbRecordAttributeDrawer : OdinAttributeDrawer<DbRecordAttribute, string>
    {
        private readonly List<string> _names = new();
        private readonly List<string> _guids = new();

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var driver = Database.GetDriver() as IDriverEditor;
            if (driver == null)
            {
                Debug.LogError("[DbRecordAttributeDrawer] Не получилось получить драйвер Базы данных.");
                return;
            }

            driver.ReloadDatabase();

            BuildList(driver);

            var btnWidth = ValueEntry.SmartValue.IsNullOrWhitespace() ? 0f : 40f;
            var controlRect =
                EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);

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

            // Префикс-label + индикатор валидности
            var contentRect = EditorGUI.PrefixLabel(dropdownRect, label);

            var prevColor = GUI.color;
            var valid = TestRecord();
            if (!valid) GUI.color = Color.red;

            var currentIndex = _guids.IndexOf(ValueEntry.SmartValue ?? string.Empty);

            // Снимок guids на момент открытия попапа — список перестраивается на каждый OnGUI,
            // но callback срабатывает позже (после закрытия попап-окна). Снимок гарантирует,
            // что индекс выбора резолвится в тот же GUID, что был под курсором.
            var guidsSnapshot = _guids.ToArray();

            SearchablePopup.Draw(contentRect, _names.ToArray(), currentIndex, null, picked =>
            {
                var newGuid = picked >= 0 && picked < guidsSnapshot.Length
                    ? guidsSnapshot[picked]
                    : string.Empty;

                if (newGuid != ValueEntry.SmartValue)
                    ValueEntry.SmartValue = newGuid;
            });

            GUI.color = prevColor;

            if (!ValueEntry.SmartValue.IsNullOrWhitespace() && GUI.Button(buttonRect, "Find"))
                FindRecordAsset(ValueEntry.SmartValue);
        }

        /// <summary>
        /// Перестраивает параллельные списки <see cref="_names"/> + <see cref="_guids"/>
        /// под текущие настройки атрибута. Имя записи преобразуется заменой "." → "/" —
        /// SearchablePopup строит из этого иерархию групп.
        /// </summary>
        private void BuildList(IDriverEditor driver)
        {
            _names.Clear();
            _guids.Clear();

            if (Attribute.RecordType == null || Attribute.RecordType == RecordTypes.Singleton)
            {
                var list = Attribute.RecordClass != null
                    ? Database.GetRecords(Attribute.RecordClass)
                    : Database.GetRecords();
                foreach (var record in list)
                {
                    _names.Add(record.Name.Replace(".", "/"));
                    _guids.Add(record.GuidPreset);
                }
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
                        continue;
                    }

                    if (Attribute.RecordClass != null &&
                        !Attribute.RecordClass.IsAssignableFrom(record.GetData().GetType()))
                        continue;

                    _names.Add(record.Name.Replace(".", "/"));
                    _guids.Add(record.GuidPreset);
                }
            }
        }

        /// <summary>Текущий GUID валиден — запись с таким ID существует в Database.</summary>
        private bool TestRecord() =>
            !ValueEntry.SmartValue.IsNullOrWhitespace() && Database.TestRecord(ValueEntry.SmartValue);

        /// <summary>Выделяет в Project View ассет пресета, соответствующий указанному GUID.</summary>
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
