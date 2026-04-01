#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Attributes
{
    [CustomPropertyDrawer(typeof(StateSwitcherAttribute))]
    public class StateSwitcherDrawer : MultiDrawer
    {
        private static GUIStyle _grayStyle;

        private static GUIStyle GrayStyle => _grayStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.gray }
        };

        private float _h;

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var switcherAttr = (StateSwitcherAttribute)attribute;
            var property = data.Property;

            // Кнопку не рисуем если поле не ObjectReference, нет состояний или не пролинкован свитчер
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return;

            var states = switcherAttr.States;
            if (states == null || states.Length == 0)
                return;

            var switcher = property.objectReferenceValue as UIStateSwitcher;
            if (switcher == null)
                return;

            // Кнопка Sync справа от поля — сужаем область поля
            float buttonWidth = 40f;
            float gap = 2f;
            float fieldWidth = data.Position.width - buttonWidth - gap;

            data.Position = new Rect(data.Position.x, data.Position.y, fieldWidth, data.Position.height);

            var buttonRect = new Rect(
                data.Position.xMax + gap,
                data.Position.y,
                buttonWidth,
                data.Position.height);

            //var syncIcon = EditorGUIUtility.IconContent("d_Refresh");
            var old = GUI.backgroundColor;
            GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);
            var style = EditorStyles.miniButton;
            style.normal.textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive);
            style.hover.textColor = ToolsSettings.GetLineColor(DefaultColors.TextColor);
            if (GUI.Button(buttonRect, "Sync", style))
            {
                SyncStates(switcher, states);
            }

            GUI.backgroundColor = old;
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool b)
        {
            var switcherAttr = (StateSwitcherAttribute)attribute;
            var property = data.Property;

            // Проверяем что поле — ссылка на UIStateSwitcher (ObjectReference)
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                var errMsg = "[StateSwitcher] Атрибут поддерживает только поля типа UIStateSwitcher.";
                var errHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(errMsg), data.Position.width);
                EditorGUI.HelpBox(
                    new Rect(data.Position.x, data.Position.y, data.Position.width, errHeight),
                    errMsg, MessageType.Error);
                return errHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            var states = switcherAttr.States;
            if (states == null || states.Length == 0)
                return 0;

            // Получаем текущий UIStateSwitcher для подсветки активного состояния
            var switcher = property.objectReferenceValue as UIStateSwitcher;
            int currentState = switcher != null ? switcher.State : -1;

            // Считаем высоту таблицы хинтов
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float totalHeight = states.Length * (lineHeight + spacing);

            var startY = data.Position.y;

            var rect = new Rect(data.Position.x, data.Position.y, data.Position.width, totalHeight);
            EditorGUI.DrawRect(rect, ToolsSettings.GetBgColor(DefaultColors.BadgeBg));
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.BorderColor));
            rect.y -= 1;
            rect.height += lineHeight + 2f;
            rect.x -= 1;
            rect.width += 2;
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetLineColor(DefaultColors.BorderColorLight));

            for (int i = 0; i < states.Length; i++)
            {
                var stateDesc = states[i];
                float y = startY + i * (lineHeight + spacing);

                var rowRect = new Rect(data.Position.x, y, data.Position.width, lineHeight);

                // Подсветка активного состояния
                bool isActive = switcher != null && stateDesc.State == currentState;
                if (isActive)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.6f, 0.3f, 0.25f));
                }

                // Hover-подсветка и клик — переключение свитчера
                if (switcher != null)
                {
                    EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

                    if (Event.current.type == EventType.MouseDown
                        && Event.current.button == 0
                        && rowRect.Contains(Event.current.mousePosition))
                    {
                        Undo.RecordObject(switcher, "Switch UIStateSwitcher State");
                        switcher.Set(stateDesc.State);
                        EditorUtility.SetDirty(switcher);
                        Event.current.Use();
                    }

                    // Лёгкая подсветка при наведении (если не активный)
                    if (!isActive && rowRect.Contains(Event.current.mousePosition))
                    {
                        EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));
                    }
                }

                // Колонка индекса
                float indexWidth = 30f;
                var indexRect = new Rect(data.Position.x, y, indexWidth, lineHeight);
                EditorGUI.LabelField(indexRect, $"{stateDesc.State}:");

                // Колонка описания
                float descX = data.Position.x + indexWidth + 4f;
                float descWidth = data.Position.width * 0.4f;
                var descRect = new Rect(descX, y, descWidth, lineHeight);
                EditorGUI.LabelField(descRect,
                    stateDesc.Description.IsNullOrWhitespace() ? "\"\"" : stateDesc.Description);

                // Колонка текущего имени состояния из свитчера (если назначен)
                float nameX = descX + descWidth + 8f;
                float nameWidth = data.Position.width - (nameX - data.Position.x);
                var nameRect = new Rect(nameX, y, nameWidth, lineHeight);

                if (switcher != null && switcher.States != null &&
                    stateDesc.State >= 0 && stateDesc.State < switcher.States.Length)
                {
                    string stateName = switcher.States[stateDesc.State].Name;
                    var style = isActive ? EditorStyles.boldLabel : EditorStyles.label;
                    EditorGUI.LabelField(nameRect, $"«{stateName}»", style);
                }
                else
                    EditorGUI.LabelField(nameRect, "[None]", GrayStyle);
            }

            return totalHeight;
        }

        // ════════════════════════════════════════════════════════
        //  Sync
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Синхронизирует состояния UIStateSwitcher с описанием из атрибута:
        /// — добавляет недостающие элементы в конец (существующие не удаляются)
        /// — удаляет лишние с конца
        /// — выставляет имена по Description из enum
        /// </summary>
        private static void SyncStates(UIStateSwitcher switcher, StateSwitcherAttribute.StateDesc[] desiredStates)
        {
            Undo.RecordObject(switcher, "Sync UIStateSwitcher States");

            var so = new SerializedObject(switcher);
            var statesArray = so.FindProperty("states");

            if (statesArray == null || !statesArray.isArray)
            {
                Debug.LogError("[StateSwitcherDrawer] Не найдено сериализованное поле 'states' в UIStateSwitcher.");
                return;
            }

            int desired = desiredStates.Length;
            int current = statesArray.arraySize;

            // Добавляем недостающие элементы в конец (существующие не трогаем)
            if (current < desired)
            {
                for (int i = current; i < desired; i++)
                    statesArray.InsertArrayElementAtIndex(i);
            }
            // Удаляем лишние с конца
            else if (current > desired)
            {
                for (int i = current - 1; i >= desired; i--)
                    statesArray.DeleteArrayElementAtIndex(i);
            }

            // Выставляем имена по Description из enum
            for (int i = 0; i < desired; i++)
            {
                var element = statesArray.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("name");

                if (nameProp != null)
                    nameProp.stringValue = desiredStates[i].Description;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(switcher);

            Debug.Log($"[StateSwitcherDrawer] Синхронизировано: {desired} состояний в '{switcher.name}'.");
        }
    }
}
#endif