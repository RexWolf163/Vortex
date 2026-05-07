#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Attributes
{
    /// <summary>
    /// Odin-drawer для <see cref="StateSwitcherAttribute"/>.
    /// Над полем UIStateSwitcher рисует таблицу описанных состояний с подсветкой активного,
    /// клик по строке переключает свитчер. Справа от поля — кнопка Sync.
    /// </summary>
    public sealed class StateSwitcherDrawer : OdinAttributeDrawer<StateSwitcherAttribute, UIStateSwitcher>
    {
        private static GUIStyle _grayStyle;

        private static GUIStyle GrayStyle => _grayStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.gray }
        };

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var states = Attribute.States;

            // Топпер: таблица состояний
            if (states != null && states.Length > 0)
                DrawStatesHint(states);

            // Поле + кнопка Sync
            DrawFieldRow(label, states);
        }

        private void DrawStatesHint(StateSwitcherAttribute.StateDesc[] states)
        {
            var switcher = ValueEntry.SmartValue;
            int currentState = switcher != null ? switcher.State : -1;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float totalHeight = states.Length * (lineHeight + spacing);

            var area = EditorGUILayout.GetControlRect(false, totalHeight);

            EditorGUI.DrawRect(area, ToolsSettings.GetBgColor(DefaultColors.BadgeBg));
            DrawingUtility.DrawBoxBorder(area, ToolsSettings.GetLineColor(DefaultColors.BorderColor));

            for (int i = 0; i < states.Length; i++)
            {
                var stateDesc = states[i];
                float y = area.y + i * (lineHeight + spacing);

                var rowRect = new Rect(area.x, y, area.width, lineHeight);

                bool isActive = switcher != null && stateDesc.State == currentState;
                if (isActive)
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.6f, 0.3f, 0.25f));

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

                    if (!isActive && rowRect.Contains(Event.current.mousePosition))
                        EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));
                }

                float indexWidth = 30f;
                var indexRect = new Rect(area.x, y, indexWidth, lineHeight);
                EditorGUI.LabelField(indexRect, $"{stateDesc.State}:");

                float descX = area.x + indexWidth + 4f;
                float descWidth = area.width * 0.4f;
                var descRect = new Rect(descX, y, descWidth, lineHeight);
                EditorGUI.LabelField(descRect,
                    stateDesc.Description.IsNullOrWhitespace() ? "\"\"" : stateDesc.Description);

                float nameX = descX + descWidth + 8f;
                float nameWidth = area.width - (nameX - area.x);
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
        }

        private void DrawFieldRow(GUIContent label, StateSwitcherAttribute.StateDesc[] states)
        {
            var switcher = ValueEntry.SmartValue;
            bool canSync = switcher != null && states != null && states.Length > 0;

            if (!canSync)
            {
                CallNextDrawer(label);
                return;
            }

            const float buttonWidth = 40f;
            const float gap = 2f;

            var rowRect = EditorGUILayout.GetControlRect();
            var fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - buttonWidth - gap, rowRect.height);
            var buttonRect = new Rect(fieldRect.xMax + gap, rowRect.y, buttonWidth, rowRect.height);

            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.ObjectField(fieldRect, label ?? GUIContent.none,
                ValueEntry.SmartValue, typeof(UIStateSwitcher), true) as UIStateSwitcher;
            if (EditorGUI.EndChangeCheck())
                ValueEntry.SmartValue = newValue;

            var old = GUI.backgroundColor;
            GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);
            var style = EditorStyles.miniButton;
            style.normal.textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive);
            style.hover.textColor = ToolsSettings.GetLineColor(DefaultColors.TextColor);
            if (GUI.Button(buttonRect, "Sync", style))
                SyncStates(switcher, states);
            GUI.backgroundColor = old;
        }

        // ════════════════════════════════════════════════════════
        //  Sync
        // ════════════════════════════════════════════════════════

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

            if (current < desired)
            {
                for (int i = current; i < desired; i++)
                {
                    statesArray.InsertArrayElementAtIndex(i);
                    // InsertArrayElementAtIndex клонирует предыдущий элемент со ВСЕМИ SerializeReference rid'ами,
                    // поэтому новый слот ссылается на те же C# объекты. Разрываем shared-ссылки —
                    // каждый stateItem заменяем на независимую копию через Clone().
                    var inserted = statesArray.GetArrayElementAtIndex(i);
                    DetachSharedReferences(inserted.FindPropertyRelative("stateItems"));
                }
            }
            else if (current > desired)
                for (int i = current - 1; i >= desired; i--)
                    statesArray.DeleteArrayElementAtIndex(i);

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

        /// <summary>
        /// Заменяет все SerializeReference-элементы массива на независимые копии через StateItem.Clone().
        /// Нужно после InsertArrayElementAtIndex, который клонирует rid'ы и создаёт shared ссылки.
        /// </summary>
        private static void DetachSharedReferences(SerializedProperty arrayProp)
        {
            if (arrayProp == null || !arrayProp.isArray) return;

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                if (element.propertyType != SerializedPropertyType.ManagedReference) continue;

                if (element.managedReferenceValue is StateItem original)
                    element.managedReferenceValue = original.Clone();
            }
        }
    }
}
#endif
