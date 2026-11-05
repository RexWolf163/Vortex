using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Naninovel;
using UnityEditor;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources
{
    /// <summary>
    /// Сборка карты связей: конфиг → сценарии <c>.nani</c> → квесты Vortex → переменные индекса. Ошибка одного ассета
    /// не обрывает сборку — она уходит в проблемы. Карта собирается целиком в локальных коллекциях и отдаётся только
    /// по окончании; отмена в индикаторе прогресса возвращает null. Зависимость односторонняя: сборщик и источники
    /// знают модель, модель о них не знает.
    /// </summary>
    internal static class VariableIndexBuilder
    {
        private const string ProgressTitle = "Custom Variables Dashboard";

        /// <summary>
        /// Собрать заново и сделать текущей картой. Отменённая сборка оставляет прежнюю карту. Возвращает true, если
        /// карта заменена.
        /// </summary>
        public static bool Rebuild()
        {
            var index = Build();
            if (index == null)
                return false;
            VariableIndex.Current = index;
            return true;
        }

        public static VariableIndex Build()
        {
            var links = new List<VariableLink>();
            var problems = new List<BuildProblem>();
            try
            {
                var declarations = CollectDeclarations(problems);

                var scripts = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                                && p.EndsWith(".nani", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();
                for (var i = 0; i < scripts.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, scripts[i], 0.8f * i / scripts.Count))
                        return null;
                    CollectScript(scripts[i], links, problems);
                }

                var questsEnabled = QuestSource.Enabled;
                var scannedQuests = 0;
                if (questsEnabled)
                {
                    scannedQuests = QuestSource.Collect(links, problems, (progress, path) =>
                        EditorUtility.DisplayCancelableProgressBar(ProgressTitle, path, 0.8f + 0.2f * progress));
                    if (scannedQuests < 0)
                        return null;
                }

                return new VariableIndex(DateTime.Now, BuildEntries(declarations, links), problems, scripts.Count,
                    scannedQuests, questsEnabled);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<VariableDeclaration> CollectDeclarations(List<BuildProblem> problems)
        {
            var config = Configuration.GetOrDefault<CustomVariablesConfiguration>();
            var configPath = AssetDatabase.GetAssetPath(config);
            var result = new List<VariableDeclaration>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var predefine in config.PredefinedVariables)
            {
                if (string.IsNullOrWhiteSpace(predefine.Name))
                {
                    problems.Add(new BuildProblem(LinkSource.Config, configPath, 0, "Объявление с пустым именем — пропущено"));
                    continue;
                }

                if (!names.Add(predefine.Name))
                {
                    problems.Add(new BuildProblem(LinkSource.Config, configPath, 0,
                        $"Повторное объявление «{predefine.Name}» — учтено первое"));
                    continue;
                }

                result.Add(new VariableDeclaration(predefine.Name, predefine.Expression));
            }

            return result;
        }

        private static void CollectScript(string path, List<VariableLink> links, List<BuildProblem> problems)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                problems.Add(new BuildProblem(LinkSource.Nani, path, 0, $"Сценарий не прочитан: {e.Message}"));
                return;
            }

            NaniScriptSource.Collect(path, text, links, problems);
        }

        /// <summary>
        /// Переменные индекса: каждое объявление даёт переменную (даже без связей), каждая связь лежит ровно в одной
        /// переменной с посимвольно тем же именем.
        /// </summary>
        internal static Dictionary<string, VariableEntry> BuildEntries(List<VariableDeclaration> declarations,
            List<VariableLink> links)
        {
            var entries = new Dictionary<string, VariableEntry>(StringComparer.Ordinal);
            foreach (var declaration in declarations)
                entries[declaration.Name] = new VariableEntry(declaration.Name) { Declaration = declaration };

            foreach (var link in links)
            {
                if (!entries.TryGetValue(link.VariableName, out var entry))
                    entries[link.VariableName] = entry = new VariableEntry(link.VariableName);
                entry.AddLink(link);
            }

            return entries;
        }
    }
}
