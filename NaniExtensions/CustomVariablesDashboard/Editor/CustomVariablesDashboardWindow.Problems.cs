using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor
{
    /// <summary>
    /// Вкладка «Проблемы»: то, что не удалось прочитать при сборке. Клик — переход к ассету (сценарий — на строку).
    /// </summary>
    internal sealed partial class CustomVariablesDashboardWindow
    {
        private Vector2 _problemsScroll;

        private void DrawProblems()
        {
            var index = DrawBuildBar();
            if (index == null)
                return;

            if (index.Problems.Count == 0)
            {
                EditorGUILayout.HelpBox("Проблем при сборке нет.", MessageType.Info);
                return;
            }

            _problemsScroll = EditorGUILayout.BeginScrollView(_problemsScroll);
            foreach (var problem in index.Problems)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(SourceTitle(problem.Source), EditorStyles.miniLabel, GUILayout.Width(70));
                    var place = problem.Line > 0 ? $"{problem.AssetPath}:{problem.Line}" : problem.AssetPath;
                    if (GUILayout.Button(place, EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                        problem.Open();
                    GUILayout.Label(problem.Message);
                }

            EditorGUILayout.EndScrollView();
        }

        private static string SourceTitle(LinkSource source) => source switch
        {
            LinkSource.Nani => ".nani",
            LinkSource.Quest => "квест",
            _ => "конфиг"
        };
    }
}
