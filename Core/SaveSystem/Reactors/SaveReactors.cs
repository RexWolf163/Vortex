using System;
using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.SaveSystem.Reactors
{
    /// <summary>
    /// Прогон реакторов над телом сейва. Драйвер вызывает <see cref="Apply"/> между распаковкой и разбором,
    /// передавая версию сборки из сводки сейва и список реакторов. Список и порядок — из <c>SaveSettings</c>,
    /// по умолчанию он пуст. Список приходит параметром, а не из <c>Settings.Data()</c>: расширение модели
    /// настроек собирается в сборку настроек, а она о SaveSystem не знает — ссылка дала бы цикл сборок.
    ///
    /// Ошибка реактора не глотается: коррекция прекращается, дальше загрузка идёт с той строкой, которая
    /// получилась до сбойного блока, а исключение уходит в лог — по нему и разбирают проблему.
    /// </summary>
    public static class SaveReactors
    {
        private const string Source = "SaveReactors";

        /// <summary>
        /// Прогоняет подходящие реакторы по порядку. Реакторов нет или ни один не подходит по версии —
        /// строка возвращается как есть.
        /// </summary>
        /// <param name="raw">Распакованное тело сейва до разбора.</param>
        /// <param name="saveVersion">Версия сборки из сводки сейва. Пусто — сейв считается самым старым.</param>
        /// <param name="reactors">Список из настроек, в порядке применения.</param>
        public static string Apply(string raw, string saveVersion, IReadOnlyList<SaveReactor> reactors)
        {
            if (reactors == null || reactors.Count == 0 || string.IsNullOrEmpty(raw))
                return raw;

            var applied = 0;
            foreach (var reactor in reactors)
            {
                if (reactor == null || !reactor.IsApplicable(saveVersion))
                    continue;

                try
                {
                    raw = reactor.TransformRaw(raw);
                    applied++;
                }
                catch (Exception e)
                {
                    Log.Print(new LogData(LogLevel.Error,
                        $"Реактор {reactor.GetType().Name} прервал коррекцию сейва версии " +
                        $"\"{saveVersion}\": {e.Message}\n{e.StackTrace}", Source));
                    return raw;
                }
            }

            if (applied > 0)
                Log.Print(new LogData(LogLevel.Common,
                    $"Сейв версии \"{saveVersion}\": применено реакторов — {applied}.", Source));

            return raw;
        }
    }
}
