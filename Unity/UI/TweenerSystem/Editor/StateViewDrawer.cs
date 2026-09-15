#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.Shortcuts;

namespace Vortex.Unity.UI.TweenerSystem.Editor
{
    /// <summary>
    /// Odin-drawer для <see cref="StateView{TEnum}"/> (через основу <see cref="StateViewBase"/>).
    /// Над полем — таблица состояний, как у <see cref="StateSwitcherAttribute"/>: активное подсвечено, клик по строке
    /// переключает (хаб выбранного — Forward, остальные — Back), справа — назначенный хаб. Кнопка Sync подгоняет
    /// массив хабов под enum и пустым состояниям создаёт дочерние слои <c>[{поле}_{состояние}_Tween]</c> с хабом.
    /// </summary>
    public sealed class StateViewDrawer<T> : OdinValueDrawer<T> where T : StateViewBase
    {
        private static GUIStyle _grayStyle;

        private static GUIStyle GrayStyle => _grayStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.gray }
        };

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var view = ValueEntry.SmartValue;
            if (view != null && view.Count > 0)
            {
                DrawStates(view);
                DrawSyncRow(view);
            }

            CallNextDrawer(label);
        }

        private Object Owner => Property.Tree.WeakTargets.Count > 0 ? Property.Tree.WeakTargets[0] as Object : null;

        private void DrawStates(StateViewBase view)
        {
            var count = view.Count;
            var current = view.CurrentIndex;
            var hubs = view.Hubs;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var area = EditorGUILayout.GetControlRect(false, count * (lineHeight + spacing));

            EditorGUI.DrawRect(area, ToolsSettings.GetBgColor(DefaultColors.BadgeBg));
            DrawingUtility.DrawBoxBorder(area, ToolsSettings.GetLineColor(DefaultColors.BorderColor));

            for (var i = 0; i < count; i++)
            {
                var y = area.y + i * (lineHeight + spacing);
                var rowRect = new Rect(area.x, y, area.width, lineHeight);
                var isActive = i == current;

                if (isActive)
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.6f, 0.3f, 0.25f));

                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    SetState(view, i);
                    Event.current.Use();
                }

                if (!isActive && rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));

                const float indexWidth = 30f;
                EditorGUI.LabelField(new Rect(area.x, y, indexWidth, lineHeight), $"{i}:");

                var descX = area.x + indexWidth + 4f;
                var descWidth = area.width * 0.4f;
                var description = new StateSwitcherAttribute.StateDesc(view.ValueAt(i)).Description;
                EditorGUI.LabelField(new Rect(descX, y, descWidth, lineHeight), description);

                var nameX = descX + descWidth + 8f;
                var nameRect = new Rect(nameX, y, area.width - (nameX - area.x), lineHeight);
                var hub = i < hubs.Length ? hubs[i] : null;
                if (hub != null)
                    EditorGUI.LabelField(nameRect, $"«{hub.name}»", isActive ? EditorStyles.boldLabel : EditorStyles.label);
                else
                    EditorGUI.LabelField(nameRect, "[None]", GrayStyle);
            }
        }

        private void DrawSyncRow(StateViewBase view)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var old = GUI.backgroundColor;
                GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);
                var style = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive) },
                    hover = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColor) }
                };
                if (GUILayout.Button("Sync", style, GUILayout.Width(40f)))
                    Sync(view);
                GUI.backgroundColor = old;
            }
        }

        private void SetState(StateViewBase view, int index)
        {
            var owner = Owner;
            if (owner != null)
                Undo.RecordObject(owner, "Switch StateView State");

            view.SetIndex(index, false);

            if (owner != null)
                EditorUtility.SetDirty(owner);
        }

        /// <summary>
        /// Длина массива — по числу состояний; пустым состояниям — дочерние слои владельца с хабом, как по
        /// Ctrl+Alt+T. Всё — одним шагом Undo.
        /// </summary>
        private void Sync(StateViewBase view)
        {
            if (Owner is not Component owner)
            {
                Debug.LogError("[StateView] Sync: владелец поля — не компонент объекта.");
                return;
            }

            if (EditorUtility.IsPersistent(owner))
            {
                Debug.LogError($"[StateView] Sync: «{owner.name}» — ассет префаба. Откройте префаб, чтобы создать слои.", owner);
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.RecordObject(owner, "Sync StateView");

            var field = Property.Name;
            var filled = view.FillEmpty(i =>
                ComponentShortcuts.CreateLayer<TweenerHub>(owner.gameObject, $"[{field}_{view.NameAt(i)}_Tween]"));

            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            Undo.CollapseUndoOperations(group);

            Debug.Log($"[StateView] Синхронизировано «{field}» в «{owner.name}»: состояний {view.Count}, создано слоёв {filled}.", owner);
        }
    }
}
#endif
