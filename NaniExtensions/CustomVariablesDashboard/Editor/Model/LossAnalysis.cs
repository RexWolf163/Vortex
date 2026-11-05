using System;
using System.Collections.Generic;
using System.Linq;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model
{
    /// <summary>
    /// Виды потерь, двойники по регистру и исключения без переменной. Всё вычисляется при показе из текущей карты и
    /// текущего списка исключений и нигде не хранится — правка исключений применяется без пересборки.
    /// </summary>
    internal static class LossAnalysis
    {
        /// <summary>Виды потерь в порядке показа</summary>
        public static readonly LossKinds[] Kinds =
        {
            LossKinds.DeclaredUnused, LossKinds.UsedUndeclared, LossKinds.NeverWritten, LossKinds.NeverRead
        };

        /// <summary>Потери переменной без учёта исключений</summary>
        public static LossKinds GetRaw(VariableEntry entry)
        {
            var kinds = LossKinds.None;
            if (entry.Declaration != null && entry.Links.Count == 0)
                kinds |= LossKinds.DeclaredUnused;
            if (entry.Declaration == null && entry.Links.Count > 0)
                kinds |= LossKinds.UsedUndeclared;

            var hasWrite = entry.Links.Any(l => l.IsWrite);
            var hasRead = entry.Links.Any(l => l.IsRead);
            if (hasRead && !hasWrite)
                kinds |= LossKinds.NeverWritten;
            if (hasWrite && !hasRead)
                kinds |= LossKinds.NeverRead;
            return kinds;
        }

        /// <summary>Потери с учётом исключений: исключение убирает только указанные в нём виды</summary>
        public static LossKinds GetVisible(VariableEntry entry) =>
            GetRaw(entry) & ~DashboardSettings.instance.GetExcluded(entry.Name);

        /// <summary>Переменные секции вида потери, по имени</summary>
        public static List<VariableEntry> GetSection(VariableIndex index, LossKinds kind) =>
            index.Entries.Values
                .Where(e => (GetVisible(e) & kind) != 0)
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>Группы имён, совпадающих без учёта регистра и различных с учётом. Исключениями не скрываются</summary>
        public static List<List<VariableEntry>> GetCaseTwins(VariableIndex index) =>
            index.Entries.Values
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.OrderBy(e => e.Name, StringComparer.Ordinal).ToList())
                .OrderBy(g => g[0].Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Исключения на имена, которых нет в карте</summary>
        public static List<VariableExclusion> GetStaleExclusions(VariableIndex index) =>
            DashboardSettings.instance.Exclusions
                .Where(x => !string.IsNullOrEmpty(x.variableName) && !index.Entries.ContainsKey(x.variableName))
                .ToList();

        public static string Title(LossKinds kind) => kind switch
        {
            LossKinds.DeclaredUnused => "Объявлена, но не используется",
            LossKinds.UsedUndeclared => "Используется, но не объявлена",
            LossKinds.NeverWritten => "Не пишется",
            LossKinds.NeverRead => "Не читается",
            _ => kind.ToString()
        };
    }
}
