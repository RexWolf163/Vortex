#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Focus;

namespace Vortex.Unity.UI.VirtualCursorSystem.Editor
{
    /// <summary>
    /// Диагностическое окно: показывает LIFO-стек <see cref="FocusGroup"/>-ов, их target'ы,
    /// текущий <c>CurrentFocus</c> и <see cref="FocusGroup.RememberedFocus"/> для каждой группы.
    /// Клик по строке группы или target'а — <c>EditorGUIUtility.PingObject</c> на соответствующий
    /// GameObject (навигация к объекту в иерархии / Project).
    ///
    /// Данные берутся из <see cref="VirtualCursorBus.Focus"/> и показываются только в Play Mode
    /// (вне игры окно показывает подсказку). Авто-перерисовка — только в Play Mode, не чаще раза в секунду
    /// (тик на <see cref="EditorApplication.update"/> подключается на входе в Play Mode и снимается на выходе).
    /// Клики и наведение в окне перерисовывают его сразу, штатно.
    ///
    /// Read-only, никакого воздействия на систему — только визуализация.
    /// </summary>
    public class FocusStackWindow : EditorWindow
    {
        /// <summary>Открыть окно стека фокуса.</summary>
        [MenuItem("Tools/Vortex/Virtual Cursor/Focus Stack")]
        public static void Open()
        {
            var w = GetWindow<FocusStackWindow>("Focus Stack");
            w.minSize = new Vector2(360f, 240f);
        }

        /// <summary>Позиция прокрутки списка групп.</summary>
        private Vector2 _scroll;

        // GUIStyle нельзя создавать до первого OnGUI — ленивая инициализация.
        // Палитра различается для Pro (dark) и Personal (light) скинов, иначе на одном из
        // них текст сливается с фоном: светлые пастельные тона на светлом фоне не видно,
        // тёмные насыщенные — на тёмном.
        /// <summary>Кэш стиля текущего фокуса.</summary>
        private static GUIStyle _currentStyle;

        /// <summary>Кэш стиля запомненного фокуса.</summary>
        private static GUIStyle _rememberedStyle;

        /// <summary>Кэш стиля активной группы.</summary>
        private static GUIStyle _activeGroupStyle;

        /// <summary>Кэш стиля null-target'а.</summary>
        private static GUIStyle _missingStyle;

        /// <summary>Кэш стиля игнорируемой группы.</summary>
        private static GUIStyle _ignoredGroupStyle;

        /// <summary>Кэш стиля метки приоритета.</summary>
        private static GUIStyle _priorityChipStyle;

        /// <summary>Цвет текущего фокуса (под тему редактора).</summary>
        private static Color CurrentColor => EditorGUIUtility.isProSkin
            ? new Color(0.55f, 1f, 0.55f)       // ярко-зелёный на тёмном
            : new Color(0.1f, 0.55f, 0.1f);     // насыщенный тёмно-зелёный на светлом

        /// <summary>Цвет запомненного фокуса.</summary>
        private static Color RememberedColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.85f, 0.35f)
            : new Color(0.65f, 0.45f, 0f);

        /// <summary>Цвет активной группы.</summary>
        private static Color ActiveGroupColor => EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.9f, 1f)
            : new Color(0.1f, 0.35f, 0.75f);

        /// <summary>Цвет null-target'а.</summary>
        private static Color MissingColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.5f, 0.5f)
            : new Color(0.65f, 0.15f, 0.15f);

        /// <summary>Цвет игнорируемой группы.</summary>
        private static Color IgnoredColor => EditorGUIUtility.isProSkin
            ? new Color(0.55f, 0.55f, 0.55f)
            : new Color(0.45f, 0.45f, 0.45f);

        /// <summary>Цвет метки приоритета.</summary>
        private static Color PriorityChipColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.6f, 0.9f)
            : new Color(0.65f, 0.15f, 0.5f);

        /// <summary>Стиль текущего фокуса (ленивый).</summary>
        private static GUIStyle CurrentStyle =>
            _currentStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = CurrentColor }
            };

        /// <summary>Стиль запомненного фокуса (ленивый).</summary>
        private static GUIStyle RememberedStyle =>
            _rememberedStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = RememberedColor }
            };

        /// <summary>Стиль активной группы (ленивый).</summary>
        private static GUIStyle ActiveGroupStyle =>
            _activeGroupStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = ActiveGroupColor }
            };

        /// <summary>Стиль null-target'а (ленивый).</summary>
        private static GUIStyle MissingStyle =>
            _missingStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = MissingColor },
                fontStyle = FontStyle.Italic
            };

        /// <summary>Стиль игнорируемой группы (ленивый).</summary>
        private static GUIStyle IgnoredGroupStyle =>
            _ignoredGroupStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = IgnoredColor },
                fontStyle = FontStyle.Italic
            };

        /// <summary>Стиль метки приоритета (ленивый).</summary>
        private static GUIStyle PriorityChipStyle =>
            _priorityChipStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = PriorityChipColor },
                alignment = TextAnchor.MiddleCenter,
            };

        /// <summary>Интервал авто-перерисовки в Play Mode, секунды.</summary>
        private const double RepaintInterval = 1d;

        /// <summary>Время последней авто-перерисовки (<see cref="EditorApplication.timeSinceStartup"/>).</summary>
        private double _lastRepaint;

        /// <summary>Тик перерисовки подписан на <see cref="EditorApplication.update"/>.</summary>
        private bool _ticking;

        /// <summary>Подписка на смену режима; в Play Mode — сразу запуск тика.</summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SetTicking(EditorApplication.isPlaying);
        }

        /// <summary>Отписка от смены режима и остановка тика.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            SetTicking(false);
        }

        /// <summary>Тик только в Play Mode: вход — запуск, выход — остановка и перерисовка в подсказку.</summary>
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    SetTicking(true);
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    SetTicking(false);
                    Repaint();
                    break;
            }
        }

        /// <summary>Включить или выключить тик перерисовки (идемпотентно).</summary>
        private void SetTicking(bool on)
        {
            if (on == _ticking) return;
            _ticking = on;
            if (on)
            {
                _lastRepaint = 0d;
                EditorApplication.update += Tick;
            }
            else
            {
                EditorApplication.update -= Tick;
            }
        }

        /// <summary>Тик редактора: перерисовка не чаще раза в <see cref="RepaintInterval"/>.</summary>
        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRepaint < RepaintInterval) return;
            _lastRepaint = now;
            Repaint();
        }

        /// <summary>Шапка + стек групп (только в Play Mode).</summary>
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

        /// <summary>Тулбар: число групп в стеке и текущий фокус.</summary>
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

        /// <summary>Стек групп сверху вниз: верх окна — верх стека.</summary>
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
            // ActiveGroup вычисляется моделью с учётом Ignore (пропускает скрытые сверху вниз).
            // Нельзя считать «активная = [^1]»: топовая может быть Ignored, тогда активна
            // следующая не-Ignored под ней.
            var activeGroup = focus.ActiveGroup;

            // Отрисовка сверху вниз: верх окна = верх стека. Естественное представление —
            // что видим сейчас, то сверху.
            for (var i = groups.Count - 1; i >= 0; i--)
            {
                var group = groups[i];
                var isActive = ReferenceEquals(group, activeGroup);
                var depth = groups.Count - 1 - i;
                DrawGroup(group, isActive, depth, current);
                EditorGUILayout.Space(2f);
            }
        }

        /// <summary>
        /// Блок группы: приоритет, статус (IGNORED / ACTIVE / глубина), запомненный фокус и target'ы.
        /// Клик по заголовку — ping группы.
        /// </summary>
        private void DrawGroup(FocusGroup group, bool isActive, int depth, IFocusTarget current)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Priority-chip слева — отдельная «метка» фиксированной ширины, чтобы
                    // приоритеты визуально выстраивались в колонку между группами.
                    var priority = group != null ? group.Priority : 0;
                    GUILayout.Label($"P{priority}", PriorityChipStyle, GUILayout.Width(28f));

                    var ignored = group != null && group.Ignore;
                    string prefix;
                    GUIStyle style;
                    if (ignored)
                    {
                        prefix = "⊘ IGNORED";
                        style = IgnoredGroupStyle;
                    }
                    else if (isActive)
                    {
                        prefix = "▶ ACTIVE";
                        style = ActiveGroupStyle;
                    }
                    else
                    {
                        prefix = $"  depth +{depth}";
                        style = EditorStyles.boldLabel;
                    }

                    var label = group == null ? "(null)" : group.name;
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

        /// <summary>
        /// Строка target'а с маркером: ● текущий фокус, ◉ запомненный, • прочие; [inactive] — не готов
        /// принимать фокус. Клик — ping объекта.
        /// </summary>
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

        /// <summary>Подпись target'а: имя GameObject'а для компонента, иначе имя типа.</summary>
        private static string TargetLabel(IFocusTarget target)
        {
            if (target == null) return "<null>";
            // IFocusTarget не обязан быть Component, но на практике единственный известный
            // имплементатор — FocusTargetComponent, который Component. Если нет — fallback
            // на тип интерфейса, пользователь увидит что-то осмысленное.
            if (target is Component c && c != null) return c.gameObject.name;
            return target.GetType().Name;
        }

        /// <summary>Подсветить и выделить GameObject группы.</summary>
        private static void PingGroup(FocusGroup group)
        {
            if (group == null) return;
            EditorGUIUtility.PingObject(group.gameObject);
            Selection.activeGameObject = group.gameObject;
        }

        /// <summary>Подсветить и выделить GameObject target'а (если target — компонент).</summary>
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
