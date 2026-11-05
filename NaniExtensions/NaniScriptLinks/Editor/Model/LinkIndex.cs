using System;
using System.Collections.Generic;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Model
{
    /// <summary>
    /// Карта связей — результат одной сборки. Живёт только в памяти редактора: статическое поле сбрасывается при
    /// перезагрузке домена (перекомпиляция, вход в Play Mode), на диск не пишется. Сборка
    /// (<c>Sources.LinkIndexBuilder</c>) заменяет карту целиком или не меняет её. Сироты, неиспользуемые метки и
    /// ненужные исключения здесь не хранятся — их считает <see cref="LinkAnalysis"/> с учётом текущих исключений.
    /// </summary>
    internal sealed class LinkIndex
    {
        /// <summary>Текущая карта; null — сборки ещё не было. Задаёт только сборщик по окончании сборки</summary>
        public static LinkIndex Current { get; internal set; }

        public DateTime BuiltAt { get; }

        /// <summary>Сценарии реестра по пути, с учётом регистра</summary>
        public IReadOnlyDictionary<string, ScriptEntry> Scripts { get; }

        public IReadOnlyList<BrokenLink> Broken { get; }

        public IReadOnlyList<BuildProblem> Problems { get; }

        /// <summary>Всего связей, принятых точками входа</summary>
        public int LinkCount { get; }

        public int ScannedQuests { get; }

        /// <summary>Источник квестов Vortex подключён (<c>USING_VORTEX_QUESTS</c>)</summary>
        public bool QuestsEnabled { get; }

        public LinkIndex(DateTime builtAt, IReadOnlyDictionary<string, ScriptEntry> scripts,
            IReadOnlyList<BrokenLink> broken, IReadOnlyList<BuildProblem> problems, int linkCount, int scannedQuests,
            bool questsEnabled)
        {
            BuiltAt = builtAt;
            Scripts = scripts;
            Broken = broken;
            Problems = problems;
            LinkCount = linkCount;
            ScannedQuests = scannedQuests;
            QuestsEnabled = questsEnabled;
        }
    }
}
