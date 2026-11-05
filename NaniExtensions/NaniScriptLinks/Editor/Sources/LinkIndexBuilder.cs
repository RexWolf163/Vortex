using System;
using System.Collections.Generic;
using System.Linq;
using Naninovel;
using UnityEditor;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Sources
{
    /// <summary>
    /// Сборка карты связей: реестр сценариев → метки и связи сценариев → запуски квестов → стартовые сценарии настроек
    /// Naninovel → разрешение каждой связи по индексу точек входа. Связь находит точку — принимается ею, не находит —
    /// битая ссылка. Сопоставление — только по полному пути реестра и метке, с учётом регистра; совпадение без учёта
    /// регистра — отдельный вид битой ссылки.
    ///
    /// Ошибка одного ассета не обрывает сборку — она уходит в проблемы. Карта собирается в локальных коллекциях и
    /// отдаётся только по окончании; отмена в индикаторе прогресса возвращает null. Ассеты проекта не меняются.
    /// </summary>
    internal static class LinkIndexBuilder
    {
        private const string ProgressTitle = "Nani Script Links";

        /// <summary>
        /// Собрать заново и сделать текущей картой. Отменённая сборка оставляет прежнюю карту. Возвращает true, если
        /// карта заменена.
        /// </summary>
        public static bool Rebuild()
        {
            var index = Build();
            if (index == null)
                return false;
            LinkIndex.Current = index;
            return true;
        }

        private static LinkIndex Build()
        {
            var links = new List<ScriptLink>();
            var broken = new List<BrokenLink>();
            var problems = new List<BuildProblem>();
            try
            {
                var assets = new Dictionary<string, Script>(StringComparer.Ordinal);
                var scripts = ScriptSource.CollectRegistry(problems, assets);

                var i = 0;
                foreach (var entry in scripts.Values)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, entry.AssetPath,
                            0.8f * i++ / scripts.Count))
                        return null;
                    try
                    {
                        ScriptSource.Collect(entry, assets[entry.Path], links, problems);
                    }
                    catch (Exception e)
                    {
                        problems.Add(new BuildProblem($"Сценарий не разобран: {e.Message}",
                            new ScriptLocation(entry.Path, entry.AssetPath, 0)));
                    }
                }

                var questsEnabled = QuestSource.Enabled;
                var scannedQuests = 0;
                if (questsEnabled)
                {
                    scannedQuests = QuestSource.Collect(links, broken, problems, (progress, path) =>
                        EditorUtility.DisplayCancelableProgressBar(ProgressTitle, path, 0.8f + 0.2f * progress));
                    if (scannedQuests < 0)
                        return null;
                }

                CollectConfig(links);
                var accepted = Resolve(scripts, links, broken);
                CollectDuplicateLabels(scripts, broken);

                return new LinkIndex(DateTime.Now, scripts,
                    broken.OrderBy(b => b.Location.Describe(), StringComparer.Ordinal).ToList(),
                    problems, accepted, scannedQuests, questsEnabled);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Стартовые сценарии настроек Naninovel: заданное поле — связь-запуск</summary>
        private static void CollectConfig(List<ScriptLink> links)
        {
            var config = Configuration.GetOrDefault<ScriptsConfiguration>();
            var configPath = AssetDatabase.GetAssetPath(config);
            Add(nameof(ScriptsConfiguration.InitializationScript), config.InitializationScript);
            Add(nameof(ScriptsConfiguration.TitleScript), config.TitleScript);
            Add(nameof(ScriptsConfiguration.StartGameScript), config.StartGameScript);

            void Add(string field, string scriptPath)
            {
                if (!string.IsNullOrEmpty(scriptPath))
                    links.Add(new ScriptLink(LinkSource.Config, null, scriptPath, null, field,
                        new AssetLocation(configPath, field)));
            }
        }

        /// <summary>Отдать каждую связь её точке входа; не нашедшие точку — в битые. Возвращает число принятых</summary>
        private static int Resolve(Dictionary<string, ScriptEntry> scripts, List<ScriptLink> links,
            List<BrokenLink> broken)
        {
            var ignoreCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in scripts.Keys)
                ignoreCase.TryAdd(path, path);

            var accepted = 0;
            foreach (var link in links)
            {
                if (!scripts.TryGetValue(link.TargetScript, out var entry))
                {
                    broken.Add(ignoreCase.TryGetValue(link.TargetScript, out var similar)
                        ? new BrokenLink(BrokenKind.CaseMismatch, link.Target,
                            $"{link.Command}: сценарий отличается регистром — в реестре «{similar}»", link.Location)
                        : new BrokenLink(BrokenKind.NoScript, link.Target,
                            $"{link.Command}: сценария «{link.TargetScript}» нет в реестре", link.Location));
                    continue;
                }

                if (link.TargetLabel == null)
                {
                    entry.AddIncoming(link, null);
                    accepted++;
                    continue;
                }

                if (entry.Labels.TryGetValue(link.TargetLabel, out var label))
                {
                    entry.AddIncoming(link, label);
                    accepted++;
                    continue;
                }

                var similarLabel = entry.Labels.Keys.FirstOrDefault(k =>
                    string.Equals(k, link.TargetLabel, StringComparison.OrdinalIgnoreCase));
                broken.Add(similarLabel != null
                    ? new BrokenLink(BrokenKind.CaseMismatch, link.Target,
                        $"{link.Command}: метка отличается регистром — объявлена «{similarLabel}»", link.Location)
                    : new BrokenLink(BrokenKind.NoLabel, link.Target,
                        $"{link.Command}: в сценарии «{entry.Path}» нет метки «{link.TargetLabel}»", link.Location));
            }

            return accepted;
        }

        /// <summary>Метка, объявленная в сценарии больше одного раза: переход попадёт на первую</summary>
        private static void CollectDuplicateLabels(Dictionary<string, ScriptEntry> scripts, List<BrokenLink> broken)
        {
            foreach (var entry in scripts.Values)
            foreach (var label in entry.Labels.Values)
                if (label.Lines.Count > 1)
                    broken.Add(new BrokenLink(BrokenKind.DuplicateLabel, label.Key,
                        $"Метка объявлена {label.Lines.Count} раза (строки {string.Join(", ", label.Lines)}) — " +
                        "переход попадёт на первую", new ScriptLocation(entry.Path, entry.AssetPath, label.Lines[1])));
        }
    }
}
