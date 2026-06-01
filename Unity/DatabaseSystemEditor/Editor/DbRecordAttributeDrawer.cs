#if UNITY_EDITOR
using System;
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
    ///
    /// Кеширование:
    ///  • Список (имена + GUID) собирается тяжело: <see cref="IDriverEditor.ReloadDatabase"/>
    ///    + два прохода по базе с генерацией параллельных массивов. На инспекторе с несколькими
    ///    [DbRecord]-полями и большой базой (например, Sound-каталог с сотнями записей)
    ///    это убивает FPS редактора.
    ///  • Static-кеш с TTL 1 секунда: drawer-инстансы с одинаковыми настройками атрибута
    ///    (<see cref="DbRecordAttribute.RecordType"/> + <see cref="DbRecordAttribute.RecordClass"/>)
    ///    делят результат. После TTL — пересборка с актуальным <c>ReloadDatabase()</c>.
    ///  • При смене ассетов в проекте лаг видимости — до 1 сек, что приемлемо для редактора.
    /// </summary>
    public class DbRecordAttributeDrawer : OdinAttributeDrawer<DbRecordAttribute, string>
    {
        private const double CacheTtlSeconds = 1.0;

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly RecordTypes? RecordType;
            public readonly Type RecordClass;

            public CacheKey(RecordTypes? recordType, Type recordClass)
            {
                RecordType = recordType;
                RecordClass = recordClass;
            }

            public bool Equals(CacheKey other) =>
                RecordType == other.RecordType && RecordClass == other.RecordClass;

            public override bool Equals(object obj) => obj is CacheKey o && Equals(o);
            public override int GetHashCode() => HashCode.Combine(RecordType, RecordClass);
        }

        private sealed class CacheEntry
        {
            public string[] Names;
            public string[] Guids;
            public double LastBuildTime;
        }

        private static readonly Dictionary<CacheKey, CacheEntry> Cache = new();

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var driver = Database.GetDriver() as IDriverEditor;
            if (driver == null)
            {
                Debug.LogError("[DbRecordAttributeDrawer] Не получилось получить драйвер Базы данных.");
                return;
            }

            var (names, guids) = ResolveList(Attribute, driver);

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

            var currentIndex = Array.IndexOf(guids, ValueEntry.SmartValue ?? string.Empty);

            // Снимок guids на момент открытия попапа — кеш может обновиться между
            // открытием попапа и его callback'ом. Снимок гарантирует, что индекс выбора
            // резолвится в тот же GUID, что был под курсором при открытии.
            var guidsSnapshot = guids;

            SearchablePopup.Draw(contentRect, names, currentIndex, null, picked =>
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
        /// Достаёт список (имена + GUID) из кеша или перестраивает его при истечении TTL.
        /// Ключ кеша — <c>(RecordType, RecordClass)</c>: drawer-инстансы с одинаковыми
        /// настройками атрибута делят результат, на каждое поле в инспекторе пересборка
        /// не происходит.
        /// </summary>
        private static (string[] names, string[] guids) ResolveList(
            DbRecordAttribute attribute, IDriverEditor driver)
        {
            var key = new CacheKey(attribute.RecordType, attribute.RecordClass);
            var now = EditorApplication.timeSinceStartup;

            if (!Cache.TryGetValue(key, out var entry))
            {
                entry = new CacheEntry();
                Cache[key] = entry;
            }

            if (entry.Names == null || (now - entry.LastBuildTime) > CacheTtlSeconds)
            {
                // ReloadDatabase делается только когда реально пересобираем кеш —
                // не на каждый OnGUI, как раньше.
                driver.ReloadDatabase();
                BuildLists(attribute, driver, out entry.Names, out entry.Guids);
                entry.LastBuildTime = now;
            }

            return (entry.Names, entry.Guids);
        }

        /// <summary>
        /// Собирает параллельные массивы имён и GUID'ов под текущие настройки атрибута.
        /// Имя записи преобразуется заменой "." → "/" — SearchablePopup строит из этого
        /// иерархию групп.
        /// </summary>
        private static void BuildLists(
            DbRecordAttribute attribute, IDriverEditor driver,
            out string[] names, out string[] guids)
        {
            var n = new List<string>();
            var g = new List<string>();

            if (attribute.RecordType == null || attribute.RecordType == RecordTypes.Singleton)
            {
                var list = attribute.RecordClass != null
                    ? Database.GetRecords(attribute.RecordClass)
                    : Database.GetRecords();
                foreach (var record in list)
                {
                    n.Add(record.Name.Replace(".", "/"));
                    g.Add(record.GuidPreset);
                }
            }

            if (attribute.RecordType == null || attribute.RecordType == RecordTypes.MultiInstance)
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

                    if (attribute.RecordClass != null &&
                        !attribute.RecordClass.IsAssignableFrom(record.GetData().GetType()))
                        continue;

                    n.Add(record.Name.Replace(".", "/"));
                    g.Add(record.GuidPreset);
                }
            }

            names = n.ToArray();
            guids = g.ToArray();
        }

        /// <summary>Текущий GUID валиден — запись с таким ID существует в Database.</summary>
        private bool TestRecord() =>
            !ValueEntry.SmartValue.IsNullOrWhitespace() && Database.TestRecord(ValueEntry.SmartValue);

        /// <summary>Выделяет в Project View ассет пресета, соответствующий указанному GUID.</summary>
        private static void FindRecordAsset(string recordId)
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
