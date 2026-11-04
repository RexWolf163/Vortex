#if UNITY_EDITOR
using System.Collections.Generic;

namespace Vortex.Sdk.Quests.Diagnostics
{
    /// <summary>
    /// Результат узла дерева условий в снимке проверки.
    /// </summary>
    internal enum ConditionResult
    {
        /// <summary>Проверка до узла не дошла (short-circuit)</summary>
        NotEvaluated,
        True,
        False
    }

    /// <summary>
    /// Рантайм-след квеста: то, что знает только ядро во время выполнения и чего нет в <see cref="QuestModel"/>.
    /// Живёт до сброса контроллера; имя, состояние, Step и флаги читаются из модели — источник истины один.
    /// </summary>
    internal class QuestRuntimeTrace
    {
        /// <summary>Номер выполняемой логики; -1 — логика не выполняется</summary>
        public int CurrentLogicIndex = -1;

        /// <summary>Номер первой выполненной логики после восстановления; -1 — квест запускался обычным путём</summary>
        public int RestoreStartIndex = -1;

        public ConditionCheckSnapshot LastStartCheck;
        public ConditionCheckSnapshot LastInterruptCheck;
    }

    /// <summary>
    /// Один штатный проход проверки одного набора условий квеста. Узел адресуется путём
    /// [группа, условие, вложенное…]; блокер не хранится — это первый False в порядке обхода.
    /// </summary>
    internal class ConditionCheckSnapshot
    {
        public readonly ConditionSetKind Kind;
        public readonly int Frame;
        public readonly float Time;

        private readonly Dictionary<string, bool> _results = new();

        public ConditionCheckSnapshot(ConditionSetKind kind, int frame, float time)
        {
            Kind = kind;
            Frame = frame;
            Time = time;
        }

        internal void Set(IReadOnlyList<int> path, bool result) => _results[Key(path)] = result;

        public ConditionResult Get(IReadOnlyList<int> path) =>
            !_results.TryGetValue(Key(path), out var result)
                ? ConditionResult.NotEvaluated
                : result
                    ? ConditionResult.True
                    : ConditionResult.False;

        private static string Key(IReadOnlyList<int> path) => string.Join(".", path);
    }
}
#endif
