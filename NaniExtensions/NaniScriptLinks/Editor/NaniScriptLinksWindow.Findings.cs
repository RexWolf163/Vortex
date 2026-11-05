using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor
{
    /// <summary>
    /// Вкладки находок: сироты (по папкам), неиспользуемые метки, битые ссылки, проблемы. Клик по месту — переход к
    /// нему (сценарий — на строке во внешнем редакторе); Find — выделить файл в инспекторе; «Исключить» переносит цель
    /// в форму вкладки «Исключения», где обязательна причина. Длинный текст переносится по строкам.
    /// </summary>
    internal sealed partial class NaniScriptLinksWindow
    {
        private const float ExcludeWidth = 70f;
        private const float KindWidth = 90f;
        private const float Indent = 12f;

        /// <summary>Доля места в паре «место + сообщение»; остальное — сообщению</summary>
        private const float LocationShare = 0.4f;

        private void DrawOrphans(LinkAnalysis analysis)
        {
            if (analysis.Orphans.Count == 0)
            {
                DrawEmpty("Сирот нет: у каждого сценария есть запуск из квеста, настроек или переход из другого сценария.");
                return;
            }

            var pathWidth = RestWidth(3, Indent + FindWidth + ExcludeWidth);
            string folder = null;
            foreach (var script in analysis.Orphans)
            {
                if (!Matches(script.Path))
                    continue;

                var scriptFolder = GetFolder(script.Path);
                if (scriptFolder != folder)
                {
                    folder = scriptFolder;
                    DrawFolderHeader(folder);
                }

                var location = new ScriptLocation(script.Path, script.AssetPath, 0);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(Indent);
                    DrawLocation(location, pathWidth);
                    DrawFind(location);
                    if (GUILayout.Button("Исключить", EditorStyles.miniButton, GUILayout.Width(ExcludeWidth)))
                        PrepareExclusion(script.Path);
                }
            }
        }

        private void DrawFolderHeader(string folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(folder.Length == 0 ? "(корень)" : folder, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                // Корень папкой не исключается — это исключило бы всё
                if (folder.Length > 0 &&
                    GUILayout.Button("Исключить папку", EditorStyles.miniButton, GUILayout.Width(110)))
                    PrepareExclusion(folder);
            }
        }

        private void DrawUnusedLabels(LinkAnalysis analysis)
        {
            if (analysis.UnusedLabels.Count == 0)
            {
                DrawEmpty("Неиспользуемых меток нет: на каждую метку ведёт хотя бы один переход.");
                return;
            }

            var rest = RestWidth(3, FindWidth + ExcludeWidth);
            foreach (var label in analysis.UnusedLabels)
            {
                if (!Matches(label.Key))
                    continue;

                var script = LinkIndex.Current.Scripts[label.ScriptPath];
                var location = new ScriptLocation(script.Path, script.AssetPath, label.Lines[0]);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawLocation(location, rest * 0.6f);
                    DrawWrapped($"# {label.Name}", rest * 0.4f);
                    DrawFind(location);
                    if (GUILayout.Button("Исключить", EditorStyles.miniButton, GUILayout.Width(ExcludeWidth)))
                        PrepareExclusion(label.Key);
                }
            }
        }

        private void DrawBroken(LinkIndex index)
        {
            if (index.Broken.Count == 0)
            {
                DrawEmpty("Битых ссылок нет.");
                return;
            }

            var rest = RestWidth(3, KindWidth + FindWidth);
            foreach (var link in index.Broken)
            {
                if (!Matches(link.Location.Describe()) && !Matches(link.Target))
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(KindTitle(link.Kind), EditorStyles.miniLabel, GUILayout.Width(KindWidth));
                    DrawLocation(link.Location, rest * LocationShare);
                    DrawWrapped(link.Message, rest * (1f - LocationShare));
                    DrawFind(link.Location);
                }
            }
        }

        private void DrawProblems(LinkIndex index, LinkAnalysis analysis)
        {
            if (index.Problems.Count == 0 && analysis.StaleExclusions.Count == 0)
            {
                DrawEmpty("Проблем нет.");
                return;
            }

            var rest = RestWidth(2, FindWidth);
            foreach (var problem in index.Problems)
            {
                if (!Matches(problem.Location.Describe()) && !Matches(problem.Message))
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawLocation(problem.Location, rest * LocationShare);
                    DrawWrapped(problem.Message, rest * (1f - LocationShare));
                    DrawFind(problem.Location);
                }
            }

            foreach (var stale in analysis.StaleExclusions)
            {
                if (!Matches(stale.Exclusion.target))
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var content = new GUIContent($"Исключение «{stale.Exclusion.target}»");
                    var rect = GUILayoutUtility.GetRect(content, WrapLinkStyle, GUILayout.Width(rest * LocationShare));
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                    if (GUI.Button(rect, content, WrapLinkStyle))
                        tab = Tab.Exclusions;
                    DrawWrapped($"{stale.Message} — снимите его", rest * (1f - LocationShare));
                }
            }
        }

        private static string KindTitle(BrokenKind kind) => kind switch
        {
            BrokenKind.NoScript => "нет сценария",
            BrokenKind.NoLabel => "нет метки",
            BrokenKind.CaseMismatch => "регистр",
            BrokenKind.DuplicateLabel => "дубль метки",
            _ => "удалён ассет"
        };

        /// <summary>Папка пути реестра с завершающим <c>/</c>; корень — пустая строка</summary>
        private static string GetFolder(string scriptPath)
        {
            var slash = scriptPath.LastIndexOf('/');
            return slash < 0 ? string.Empty : scriptPath.Substring(0, slash + 1);
        }
    }
}
