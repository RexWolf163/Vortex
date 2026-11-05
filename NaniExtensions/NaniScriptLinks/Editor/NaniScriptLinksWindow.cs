using System;
using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Sources;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor
{
    /// <summary>
    /// Валидатор связей сценариев Naninovel: сироты (никто не запускает и не переходит), неиспользуемые метки, битые
    /// ссылки, проблемы сборки и исключения. Карта собирается по кнопке и живёт в памяти до перезагрузки домена
    /// (<see cref="LinkIndex"/>); окно её только показывает, действия передаёт сборщику и настройкам.
    /// </summary>
    internal sealed partial class NaniScriptLinksWindow : EditorWindow
    {
        private enum Tab
        {
            Orphans,
            UnusedLabels,
            Broken,
            Problems,
            Exclusions
        }

        [SerializeField] private Tab tab = Tab.Orphans;

        [SerializeField] private string search = string.Empty;

        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/Debug/Nani Script Links")]
        private static void Open() =>
            GetWindow<NaniScriptLinksWindow>(utility: false, title: "Nani Script Links", focus: true);

        private void OnGUI()
        {
            var index = LinkIndex.Current;
            var analysis = LinkAnalysis.Get();

            tab = (Tab)GUILayout.Toolbar((int)tab, GetTabNames(index, analysis), EditorStyles.toolbarButton);
            DrawBuildBar(index);
            if (tab != Tab.Exclusions)
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            if (tab == Tab.Exclusions)
            {
                DrawExclusions(analysis);
                return;
            }

            if (index == null)
            {
                EditorGUILayout.HelpBox("Карта связей не собрана. Нажмите «Собрать»: будут прочитаны реестр сценариев " +
                                        "Naninovel, все сценарии .nani, квесты Vortex и стартовые сценарии настроек. " +
                                        "Карта сбрасывается при перекомпиляции и входе в Play Mode.", MessageType.Info);
                return;
            }

            // Без горизонтальной прокрутки: длинный текст переносится по строкам (см. DrawWrapped)
            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none,
                GUI.skin.verticalScrollbar, GUIStyle.none);
            switch (tab)
            {
                case Tab.Orphans:
                    DrawOrphans(analysis);
                    break;
                case Tab.UnusedLabels:
                    DrawUnusedLabels(analysis);
                    break;
                case Tab.Broken:
                    DrawBroken(index);
                    break;
                case Tab.Problems:
                    DrawProblems(index, analysis);
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private static string[] GetTabNames(LinkIndex index, LinkAnalysis analysis) => index == null
            ? new[] { "Сироты", "Неиспользуемые метки", "Битые ссылки", "Проблемы", "Исключения" }
            : new[]
            {
                $"Сироты ({analysis.Orphans.Count})",
                $"Неиспользуемые метки ({analysis.UnusedLabels.Count})",
                $"Битые ссылки ({index.Broken.Count})",
                $"Проблемы ({index.Problems.Count + analysis.StaleExclusions.Count})",
                $"Исключения ({ScriptLinksSettings.instance.Exclusions.Count})"
            };

        /// <summary>Кнопка «Собрать» и сводка последней сборки</summary>
        private static void DrawBuildBar(LinkIndex index)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Собрать", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    LinkIndexBuilder.Rebuild();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label(index == null
                    ? "Не собрано"
                    : $"Собрано {index.BuiltAt:HH:mm:ss} · сценариев {index.Scripts.Count} · связей {index.LinkCount} · " +
                      (index.QuestsEnabled ? $"квестов {index.ScannedQuests}" : "квесты Vortex отключены"),
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Строка проходит поиск: подстрока без учёта регистра</summary>
        private bool Matches(string text) =>
            string.IsNullOrEmpty(search) || text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private const float FindWidth = 40f;

        /// <summary>Промежуток между элементами строки (поля стилей IMGUI)</summary>
        private const float Gap = 6f;

        private static GUIStyle _wrapStyle;
        private static GUIStyle _wrapLinkStyle;

        private static GUIStyle WrapStyle => _wrapStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

        private static GUIStyle WrapLinkStyle =>
            _wrapLinkStyle ??= new GUIStyle(EditorStyles.linkLabel) { wordWrap = true };

        /// <summary>Ширина строки списка: окно минус вертикальная прокрутка и поля</summary>
        private float RowWidth => position.width - 24f;

        /// <summary>
        /// Ширина, оставшаяся строке после элементов фиксированной ширины (с промежутками между ними).
        /// <paramref name="fixedCount"/> — число таких элементов, <paramref name="fixedWidth"/> — их суммарная ширина.
        /// </summary>
        private float RestWidth(int fixedCount, float fixedWidth) =>
            Mathf.Max(60f, RowWidth - fixedWidth - Gap * (fixedCount + 1));

        /// <summary>Текст заданной ширины; не помещается — переносится по строкам</summary>
        private static void DrawWrapped(string text, float width)
        {
            var content = new GUIContent(text);
            var rect = GUILayoutUtility.GetRect(content, WrapStyle, GUILayout.Width(width));
            GUI.Label(rect, content, WrapStyle);
        }

        /// <summary>Место — ссылкой заданной ширины с переносом; клик открывает</summary>
        private static void DrawLocation(LinkLocation location, float width)
        {
            var content = new GUIContent(location.Describe());
            var rect = GUILayoutUtility.GetRect(content, WrapLinkStyle, GUILayout.Width(width));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, content, WrapLinkStyle))
                location.Open();
        }

        /// <summary>Кнопка Find: выделить файл места в инспекторе</summary>
        private static void DrawFind(LinkLocation location)
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(location.AssetPath)))
                if (GUILayout.Button(new GUIContent("Find", "Выделить файл в инспекторе"), EditorStyles.miniButton,
                        GUILayout.Width(FindWidth)))
                    location.Find();
        }

        private static void DrawEmpty(string message) => EditorGUILayout.HelpBox(message, MessageType.Info);
    }
}
