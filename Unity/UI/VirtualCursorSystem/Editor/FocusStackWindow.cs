#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem.Editor
{
    /// <summary>
    /// Диагностическое окно: показывает LIFO-стек <see cref="FocusGroup"/>-ов, их target'ы,
    /// текущий <c>CurrentFocus</c> и <see cref="FocusGroup.RememberedFocus"/> для каждой группы.
    /// Клик по строке группы или target'а — <c>EditorGUIUtility.PingObject</c> на соответствующий
    /// GameObject (навигация к объекту в иерархии / Project).
    ///
    /// Данные берутся из <see cref="VirtualCursorBus.Focus"/>. До инициализации (Play Mode +
    /// <c>VirtualCursorBootstrap</c>) модели нет — окно показывает подсказку. Авто-repaint
    /// через <see cref="EditorApplication.update"/> ~раз на тик, т.к. состояние меняется
    /// быстро (навигация, open/close меню).
    ///
    /// Read-only, никакого воздействия на систему — только визуализация.
    /// </summary>
    public class FocusStackWindow : EditorWindow
    {
        [MenuItem("Tools/Vortex/Virtual Cursor/Focus Stack")]
        public static void Open()
        {
            var w = GetWindow<FocusStackWindow>("Focus Stack");
            w.minSize = new Vector2(360f, 240f);
        }

        private Vector2 _scroll;

        // GUIStyle нельзя создавать до первого OnGUI — ленивая инициализация.
        // Палитра различается для Pro (dark) и Personal (light) скинов, иначе на одном из
        // них текст сливается с фоном: светлые пастельные тона на светлом фоне не видно,
        // тёмные насыщенные — на тёмном.
        private static GUIStyle _currentStyle;
        private static GUIStyle _rememberedStyle;
        private static GUIStyle _activeGroupStyle;
        private static GUIStyle _missingStyle;

        private static Color CurrentColor => EditorGUIUtility.isProSkin
            ? new Color(0.55f, 1f, 0.55f)       // ярко-зелёный на тёмном
            : new Color(0.1f, 0.55f, 0.1f);     // насыщенный тёмно-зелёный на светлом

        private static Color RememberedColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.85f, 0.35f)
            : new Color(0.65f, 0.45f, 0f);

        private static Color ActiveGroupColor => EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.9f, 1f)
            : new Color(0.1f, 0.35f, 0.75f);

        private static Color MissingColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.5f, 0.5f)
            : new Color(0.65f, 0.15f, 0.15f);

        private static GUIStyle CurrentStyle =>
            _currentStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = CurrentColor }
            };

        private static GUIStyle RememberedStyle =>
            _rememberedStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = RememberedColor }
            };

        private static GUIStyle ActiveGroupStyle =>
            _activeGroupStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ActiveGroupColor }
            };

        private static GUIStyle MissingStyle =>
            _missingStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = MissingColor },
                fontStyle = FontStyle.Italic
            };

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            DrawHeader();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Фокус-модель существует только в Play Mode после инициализации VirtualCursorBootstrap.",
                    MessageType.Info);
                return;
            }

            var focus = VirtualCursorBus.Focus;
            if (focus == null)
            {
                EditorGUILayout.HelpBox(
                    "VirtualCursorFocusController ещё не инициализирован. " +
                    "Проверь, что на сцене есть VirtualCursorBootstrap.",
                    MessageType.Warning);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                DrawStack(focus);
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var focus = Application.isPlaying ? VirtualCursorBus.Focus : null;
                var groupsCount = focus?.Groups.Count ?? 0;
                var currentFocus = focus?.CurrentFocus.Value;
                var currentLabel = currentFocus == null
                    ? "<none>"
                    : TargetLabel(currentFocus);

                EditorGUILayout.LabelField(
                    $"Groups: {groupsCount}   Current: {currentLabel}",
                    EditorStyles.toolbarButton);
            }
        }

        private void DrawStack(FocusModel focus)
        {
            var groups = focus.Groups;
            if (groups.Count == 0)
            {
                EditorGUILayout.LabelField("(стек пуст — нет активных FocusGroup)",
                    EditorStyles.miniLabel);
                return;
            }

            var current = focus.CurrentFocus.Value;

            // Отрисовка сверху вниз: верх окна = верх стека (ActiveGroup). Естественное
            // представление LIFO — что видим сейчас, то сверху.
            for (var i = groups.Count - 1; i >= 0; i--)
            {
                var group = groups[i];
                var isActive = i == groups.Count - 1;
                var depth = groups.Count - 1 - i;
                DrawGroup(group, isActive, depth, current);
                EditorGUILayout.Space(2f);
            }
        }

        private void DrawGroup(FocusGroup group, bool isActive, int depth, IFocusTarget current)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var prefix = isActive ? "▶ ACTIVE" : $"  depth +{depth}";
                    var label = group == null ? "(null)" : group.name;
                    var style = isActive ? ActiveGroupStyle : EditorStyles.boldLabel;

                    if (GUILayout.Button($"{prefix}   {label}", style,
                            GUILayout.ExpandWidth(true)))
                    {
                        PingGroup(group);
                    }
                }

                if (group == null) return;

                var remembered = group.RememberedFocus;
                if (remembered != null)
                {
                    EditorGUILayout.LabelField(
                        $"  Remembered: {TargetLabel(remembered)}",
                        RememberedStyle);
                }

                var targets = group.Targets;
                if (targets.Count == 0)
                {
                    EditorGUILayout.LabelField("  (нет зарегистрированных target'ов)",
                        EditorStyles.miniLabel);
                    return;
                }

                for (var i = 0; i < targets.Count; i++)
                {
                    DrawTarget(targets[i], current, remembered);
                }
            }
        }

        private void DrawTarget(IFocusTarget target, IFocusTarget current, IFocusTarget remembered)
        {
            if (target == null)
            {
                EditorGUILayout.LabelField("  • (null)", MissingStyle);
                return;
            }

            var marker = ReferenceEquals(target, current) ? "● "
                : ReferenceEquals(target, remembered) ? "◉ "
                : "• ";
            var active = target.IsActive ? "" : " [inactive]";
            var style = ReferenceEquals(target, current) ? CurrentStyle
                : ReferenceEquals(target, remembered) ? RememberedStyle
                : EditorStyles.label;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                if (GUILayout.Button(
                        $"{marker}{TargetLabel(target)}{active}",
                        style,
                        GUILayout.ExpandWidth(true)))
                {
                    PingTarget(target);
                }
            }
        }

        private static string TargetLabel(IFocusTarget target)
        {
            if (target == null) return "<null>";
            // IFocusTarget не обязан быть Component, но на практике единственный известный
            // имплементатор — FocusTargetComponent, который Component. Если нет — fallback
            // на тип интерфейса, пользователь увидит что-то осмысленное.
            if (target is Component c && c != null) return c.gameObject.name;
            return target.GetType().Name;
        }

        private static void PingGroup(FocusGroup group)
        {
            if (group == null) return;
            EditorGUIUtility.PingObject(group.gameObject);
            Selection.activeGameObject = group.gameObject;
        }

        private static void PingTarget(IFocusTarget target)
        {
            if (target is Component c && c != null)
            {
                EditorGUIUtility.PingObject(c.gameObject);
                Selection.activeGameObject = c.gameObject;
            }
        }
    }
}
#endif
