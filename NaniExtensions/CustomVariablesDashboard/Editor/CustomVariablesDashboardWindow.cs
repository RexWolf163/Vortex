using System.Globalization;
using Naninovel;
using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor
{
    /// <summary>
    /// Панель переменных Naninovel: значения в Play Mode с правкой, карта связей переменных со сценариями <c>.nani</c>
    /// и квестами Vortex, «потеряшки» и проблемы сборки. Карта собирается по кнопке и живёт в памяти до перезагрузки
    /// домена (<see cref="VariableIndex"/>); окно её только показывает.
    /// </summary>
    internal sealed partial class CustomVariablesDashboardWindow : EditorWindow
    {
        private enum Tab
        {
            Values,
            Links,
            Losses,
            Problems
        }

        private static readonly string[] TabNames = { "Значения", "Связи", "Потеряшки", "Проблемы" };

        [SerializeField] private Tab tab = Tab.Links;

        /// <summary>Выбранная переменная на вкладке «Связи»</summary>
        [SerializeField] private string selected;

        [MenuItem("Tools/Vortex/Debug/Custom Variables Dashboard")]
        private static void Open() =>
            GetWindow<CustomVariablesDashboardWindow>(utility: false, title: "Custom Variables", focus: true);

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnsubscribeValues();
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            UnsubscribeValues();
            ClearEdits();
            Repaint();
        }

        private void OnGUI()
        {
            tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, EditorStyles.toolbarButton);
            switch (tab)
            {
                case Tab.Values:
                    DrawValues();
                    break;
                case Tab.Links:
                    DrawLinks();
                    break;
                case Tab.Losses:
                    DrawLosses();
                    break;
                case Tab.Problems:
                    DrawProblems();
                    break;
            }
        }

        /// <summary>Перейти к карточке переменной на вкладке «Связи»</summary>
        private void Select(string variableName)
        {
            selected = variableName;
            tab = Tab.Links;
            GUI.FocusControl(null);
        }

        /// <summary>Панель со временем сборки и кнопкой «Собрать»; без карты — подсказка. Возвращает карту или null</summary>
        private VariableIndex DrawBuildBar()
        {
            var index = VariableIndex.Current;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Собрать", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    VariableIndexBuilder.Rebuild();
                    index = VariableIndex.Current;
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label(index == null
                    ? "Не собрано"
                    : $"Собрано {index.BuiltAt:HH:mm:ss} · переменных {index.Entries.Count} · сценариев {index.ScannedScripts} · " +
                      (index.QuestsEnabled ? $"квестов {index.ScannedQuests}" : "квесты Vortex отключены") +
                      $" · проблем {index.Problems.Count}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            if (index == null)
                EditorGUILayout.HelpBox("Карта связей не собрана. Нажмите «Собрать»: будут прочитаны все сценарии .nani " +
                                        "и квесты Vortex проекта. Карта сбрасывается при перекомпиляции и входе в Play Mode.",
                    MessageType.Info);
            return index;
        }

        /// <summary>Значение переменной для показа: число — с точкой, логическое — true/false</summary>
        private static string Format(CustomVariableValue value) => value.Type switch
        {
            CustomVariableValueType.Numeric => value.Number.ToString(CultureInfo.InvariantCulture),
            CustomVariableValueType.Boolean => value.Boolean ? "true" : "false",
            _ => value.String
        };

        /// <summary>Менеджер переменных; вне Play Mode или до инициализации Naninovel — null</summary>
        private static ICustomVariableManager GetManager() =>
            EditorApplication.isPlaying && Engine.Initialized ? Engine.GetService<ICustomVariableManager>() : null;
    }
}
