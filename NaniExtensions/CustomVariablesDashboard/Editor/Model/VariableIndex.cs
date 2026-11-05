using System;
using System.Collections.Generic;
using System.Linq;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model
{
    /// <summary>
    /// Всё известное об одной переменной: объявление и связи. Существует, если есть объявление или хотя бы одна связь.
    /// Виды потерь не хранятся — вычисляются при показе (<see cref="LossAnalysis"/>).
    /// </summary>
    internal sealed class VariableEntry
    {
        private readonly List<VariableLink> _links = new();

        public string Name { get; }

        /// <summary>Объявление из конфига; null — не объявлена</summary>
        public VariableDeclaration Declaration { get; internal set; }

        public IReadOnlyList<VariableLink> Links => _links;

        public VariableEntry(string name) => Name = name;

        internal void AddLink(VariableLink link) => _links.Add(link);

        public int CountOf(LinkSource source) => _links.Count(l => l.Source == source);
    }

    /// <summary>
    /// Карта связей — результат одной сборки. Живёт только в памяти редактора: статическое поле сбрасывается при
    /// перезагрузке домена (перекомпиляция, вход в Play Mode), на диск не пишется. Сборка (<c>Sources.VariableIndexBuilder</c>)
    /// заменяет карту целиком или не меняет её.
    /// </summary>
    internal sealed class VariableIndex
    {
        /// <summary>Текущая карта; null — сборки ещё не было. Задаёт только сборщик по окончании сборки</summary>
        public static VariableIndex Current { get; internal set; }

        public DateTime BuiltAt { get; }

        /// <summary>Переменные по имени, сравнение с учётом регистра (как в Naninovel)</summary>
        public IReadOnlyDictionary<string, VariableEntry> Entries { get; }

        public IReadOnlyList<BuildProblem> Problems { get; }

        public int ScannedScripts { get; }

        public int ScannedQuests { get; }

        /// <summary>Источник квестов Vortex подключён (<c>USING_VORTEX_QUESTS</c>)</summary>
        public bool QuestsEnabled { get; }

        public VariableIndex(DateTime builtAt, IReadOnlyDictionary<string, VariableEntry> entries,
            IReadOnlyList<BuildProblem> problems, int scannedScripts, int scannedQuests, bool questsEnabled)
        {
            BuiltAt = builtAt;
            Entries = entries;
            Problems = problems;
            ScannedScripts = scannedScripts;
            ScannedQuests = scannedQuests;
            QuestsEnabled = questsEnabled;
        }
    }
}
