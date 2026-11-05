using System;
using System.Collections.Generic;
using System.Linq;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Model
{
    /// <summary>Исключение, которое ничего не скрывает, и почему</summary>
    internal sealed class StaleExclusion
    {
        public ScriptExclusion Exclusion { get; }

        public string Message { get; }

        public StaleExclusion(ScriptExclusion exclusion, string message)
        {
            Exclusion = exclusion;
            Message = message;
        }
    }

    /// <summary>
    /// Вывод по карте с учётом текущих исключений: сироты, неиспользуемые метки, ненужные исключения. Считается при
    /// показе и кэшируется по паре «карта + ревизия исключений» — правка исключений применяется без пересборки.
    ///
    /// Состояние точки входа проверяется по порядку: «есть входящие» → «исключена» → «сирота / неиспользуемая метка».
    /// Исключение папки или сценария покрывает и его метки. Исключение, которое ничего не скрывает (под ним нет точек
    /// входа или у всех есть входящие связи), — ненужное.
    /// </summary>
    internal sealed class LinkAnalysis
    {
        private static LinkAnalysis _cached;

        private readonly LinkIndex _index;
        private readonly int _revision;

        /// <summary>Сироты без исключённых, по пути</summary>
        public IReadOnlyList<ScriptEntry> Orphans { get; }

        /// <summary>Неиспользуемые метки без исключённых, по сценарию и строке</summary>
        public IReadOnlyList<LabelEntry> UnusedLabels { get; }

        public IReadOnlyList<StaleExclusion> StaleExclusions { get; }

        /// <summary>Анализ текущей карты; без карты — null</summary>
        public static LinkAnalysis Get()
        {
            var index = LinkIndex.Current;
            if (index == null)
                return null;
            var settings = ScriptLinksSettings.instance;
            if (_cached == null || _cached._index != index || _cached._revision != settings.Revision)
                _cached = new LinkAnalysis(index, settings);
            return _cached;
        }

        private LinkAnalysis(LinkIndex index, ScriptLinksSettings settings)
        {
            _index = index;
            _revision = settings.Revision;
            var exclusions = settings.Exclusions;

            var scripts = index.Scripts.Values.OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
            var rawOrphans = scripts.Where(s => s.ExternalIncoming.Count == 0).ToList();
            var rawUnused = scripts
                .SelectMany(s => s.Labels.Values.OrderBy(l => l.Lines[0]))
                .Where(l => l.Incoming.Count == 0)
                .ToList();

            Orphans = rawOrphans.Where(s => !exclusions.Any(x => CoversScript(x, s.Path))).ToList();
            UnusedLabels = rawUnused.Where(l => !exclusions.Any(x => CoversLabel(x, l))).ToList();
            StaleExclusions = exclusions
                .Select(x => GetStale(x, scripts, rawOrphans, rawUnused))
                .Where(s => s != null)
                .ToList();
        }

        private static bool CoversScript(ScriptExclusion exclusion, string scriptPath) => exclusion.Scope switch
        {
            ExclusionScope.Folder => scriptPath.StartsWith(exclusion.target, StringComparison.Ordinal),
            ExclusionScope.Script => exclusion.target == scriptPath,
            _ => false
        };

        private static bool CoversLabel(ScriptExclusion exclusion, LabelEntry label) =>
            exclusion.Scope == ExclusionScope.Label
                ? exclusion.target == label.Key
                : CoversScript(exclusion, label.ScriptPath);

        private static StaleExclusion GetStale(ScriptExclusion exclusion, List<ScriptEntry> scripts,
            List<ScriptEntry> rawOrphans, List<LabelEntry> rawUnused)
        {
            bool matches;
            bool hides;
            if (exclusion.Scope == ExclusionScope.Label)
            {
                matches = scripts.Any(s => s.Labels.Values.Any(l => l.Key == exclusion.target));
                hides = rawUnused.Any(l => l.Key == exclusion.target);
            }
            else
            {
                matches = scripts.Any(s => CoversScript(exclusion, s.Path));
                hides = rawOrphans.Any(s => CoversScript(exclusion, s.Path)) ||
                        rawUnused.Any(l => CoversScript(exclusion, l.ScriptPath));
            }

            if (!matches)
                return new StaleExclusion(exclusion, exclusion.Scope switch
                {
                    ExclusionScope.Folder => "Под папкой нет ни одного сценария",
                    ExclusionScope.Script => "Сценария нет в реестре",
                    _ => "Метки нет"
                });

            return hides
                ? null
                : new StaleExclusion(exclusion, "Ничего не скрывает — у всех точек входа под ним есть входящие ссылки");
        }
    }
}
