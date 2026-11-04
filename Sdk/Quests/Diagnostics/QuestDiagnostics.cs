using System.Diagnostics;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.Quests.Editor;
#endif

namespace Vortex.Sdk.Quests.Diagnostics
{
    /// <summary>
    /// Инструментация монитора квестов. Все точки записи — [Conditional("UNITY_EDITOR")]: в билде вызовы
    /// вместе с вычислением аргументов вырезаются компилятором, данные монитора существуют только в редакторе.
    ///
    /// Монитор только фиксирует то, что ядро делает штатно (INV-D1): результаты условий пишутся в момент
    /// штатного <c>Check()</c> внутри контекста проверки, открытого контроллером. Проверки вне контекста
    /// (например, UI-переключатели по условиям) в снимки квестов не попадают.
    /// </summary>
    internal static class QuestDiagnostics
    {
#if UNITY_EDITOR
        internal static readonly QuestJournal Journal = new(QuestDebugSettings.DefaultJournalCapacity);

        private static readonly Dictionary<string, QuestRuntimeTrace> Traces = new();

        /// <summary>Снимок открытого контекста проверки; null — запись условий не ведётся</summary>
        private static ConditionCheckSnapshot _check;

        private static readonly List<int> Path = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Traces.Clear();
            _check = null;
            Path.Clear();
            Journal.Reset(QuestDebugSettings.GetJournalCapacity());
        }

        internal static QuestRuntimeTrace GetTrace(string guid) => Traces.TryGetValue(guid, out var trace) ? trace : null;

        private static QuestRuntimeTrace Trace(QuestModel quest)
        {
            if (!Traces.TryGetValue(quest.GuidPreset, out var trace))
                Traces[quest.GuidPreset] = trace = new QuestRuntimeTrace();
            return trace;
        }

        private static JournalEntry NewEntry(JournalEntryKind kind) => new()
        {
            Kind = kind,
            Frame = Time.frameCount,
            Time = Time.realtimeSinceStartup
        };
#endif

        #region Журнал

        [Conditional("UNITY_EDITOR")]
        internal static void RecordTransition(QuestModel quest, QuestState from, QuestState to,
            TransitionReason reason, int logicIndex, string detail)
        {
#if UNITY_EDITOR
            var entry = NewEntry(JournalEntryKind.Transition);
            entry.QuestGuid = quest.GuidPreset;
            entry.QuestName = quest.QuestName;
            entry.From = from;
            entry.To = to;
            entry.Reason = reason;
            entry.LogicIndex = logicIndex;
            entry.Detail = detail;
            Journal.Add(entry);
#endif
        }

        /// <summary>Событие без смены состояния; quest == null — событие сессии (напр. перепроверка всех условий)</summary>
        [Conditional("UNITY_EDITOR")]
        internal static void RecordNote(QuestModel quest, TransitionReason reason, string detail = null)
        {
#if UNITY_EDITOR
            var entry = NewEntry(JournalEntryKind.Note);
            entry.QuestGuid = quest?.GuidPreset;
            entry.QuestName = quest?.QuestName;
            entry.From = entry.To = quest?.State ?? QuestState.Unset;
            entry.Reason = reason;
            entry.Detail = detail;
            Journal.Add(entry);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal static void RecordSeparator(string detail)
        {
#if UNITY_EDITOR
            var entry = NewEntry(JournalEntryKind.Separator);
            entry.Detail = detail;
            Journal.Add(entry);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal static void RecordSettleOverflow()
        {
#if UNITY_EDITOR
            var entry = NewEntry(JournalEntryKind.SettleOverflow);
            entry.Detail = "Перепроверка условий прервана предохранителем глубины";
            Journal.Add(entry);
#endif
        }

        #endregion

        #region След квеста

        /// <summary>Сброс следов: на сбросе контроллера (новая игра, загрузка, Off/Loading)</summary>
        [Conditional("UNITY_EDITOR")]
        internal static void ClearTraces()
        {
#if UNITY_EDITOR
            Traces.Clear();
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal static void SetCurrentLogic(QuestModel quest, int index)
        {
#if UNITY_EDITOR
            Trace(quest).CurrentLogicIndex = index;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal static void SetRestoreStart(QuestModel quest, int index)
        {
#if UNITY_EDITOR
            Trace(quest).RestoreStartIndex = index;
#endif
        }

        #endregion

        #region Контекст проверки условий

        /// <summary>Открывает контекст: снимок заменяет предыдущий снимок этого набора условий квеста</summary>
        [Conditional("UNITY_EDITOR")]
        internal static void BeginCheck(QuestModel quest, ConditionSetKind kind)
        {
#if UNITY_EDITOR
            _check = new ConditionCheckSnapshot(kind, Time.frameCount, Time.realtimeSinceStartup);
            Path.Clear();
            var trace = Trace(quest);
            if (kind == ConditionSetKind.Start)
                trace.LastStartCheck = _check;
            else
                trace.LastInterruptCheck = _check;
#endif
        }

        /// <summary>Закрывает контекст. Вызывать из finally — исключение в Check() не должно оставить контекст открытым</summary>
        [Conditional("UNITY_EDITOR")]
        internal static void EndCheck()
        {
#if UNITY_EDITOR
            _check = null;
            Path.Clear();
#endif
        }

        /// <summary>Вход в узел дерева условий с индексом среди соседей. Парный вызов — <see cref="ExitNode"/></summary>
        [Conditional("UNITY_EDITOR")]
        internal static void EnterNode(int index)
        {
#if UNITY_EDITOR
            if (_check == null)
                return;
            Path.Add(index);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        internal static void ExitNode(bool result)
        {
#if UNITY_EDITOR
            if (_check == null)
                return;
            _check.Set(Path, result);
            Path.RemoveAt(Path.Count - 1);
#endif
        }

        #endregion
    }
}
