#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

namespace Vortex.Sdk.Quests.Diagnostics
{
    internal enum JournalEntryKind
    {
        /// <summary>Смена состояния квеста</summary>
        Transition,

        /// <summary>Событие квеста без смены состояния (восстановление, безопасное дебаг-действие)</summary>
        Note,

        /// <summary>Граница сессии: новая игра / загрузка</summary>
        Separator,

        /// <summary>Сработал предохранитель глубины перепроверки условий</summary>
        SettleOverflow
    }

    /// <summary>
    /// Запись журнала. Неизменна; имя квеста копируется в момент записи, чтобы экспорт читался без индекса.
    /// </summary>
    internal class JournalEntry
    {
        public long Seq;
        public int Frame;
        public float Time;
        public JournalEntryKind Kind;
        public string QuestGuid;
        public string QuestName;
        public QuestState From;
        public QuestState To;
        public TransitionReason Reason;
        public int LogicIndex = -1;
        public string Detail;

        public string ToExportLine()
        {
            var quest = QuestGuid == null ? "-" : $"{QuestName} ({QuestGuid})";
            var states = Kind switch
            {
                JournalEntryKind.Transition => $"{From} → {To}",
                JournalEntryKind.Note => $"{To}",
                _ => "-"
            };
            var reason = Kind is JournalEntryKind.Transition or JournalEntryKind.Note ? Reason.ToString() : Kind.ToString();
            var logic = LogicIndex < 0 ? "-" : LogicIndex.ToString();
            return $"{Seq} | {Frame} | {Time:F3} | {quest} | {states} | {reason} | {logic} | {Detail}";
        }
    }

    /// <summary>
    /// Журнал переходов: кольцевой буфер фиксированной ёмкости. Защиты от спама нет намеренно — квест,
    /// забивающий журнал, это видимый симптом бага; вытеснение видно по <see cref="Evicted"/> и пропуску Seq.
    /// </summary>
    internal class QuestJournal
    {
        private JournalEntry[] _entries;
        private int _head;

        public int Capacity => _entries.Length;
        public int Count { get; private set; }
        public long Evicted { get; private set; }
        private long _nextSeq;

        /// <summary>
        /// Буфер рабочий сразу: контроллер пишет в журнал и вне Play Mode (сбор данных в редакторе) — раньше,
        /// чем ёмкость из настроек применится при входе в Play Mode.
        /// </summary>
        internal QuestJournal(int capacity) => Reset(capacity);

        internal void Reset(int capacity)
        {
            _entries = new JournalEntry[capacity];
            _head = 0;
            Count = 0;
            Evicted = 0;
            _nextSeq = 0;
        }

        internal void Add(JournalEntry entry)
        {
            entry.Seq = _nextSeq++;
            _entries[_head] = entry;
            _head = (_head + 1) % _entries.Length;
            if (Count < _entries.Length)
                Count++;
            else
                Evicted++;
        }

        /// <summary>Записи от старой к новой</summary>
        public IEnumerable<JournalEntry> Entries()
        {
            var start = (_head - Count + _entries.Length) % _entries.Length;
            for (var i = 0; i < Count; i++)
                yield return _entries[(start + i) % _entries.Length];
        }

        public string Export(string header)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine($"Записей: {Count}, вытеснено: {Evicted}, ёмкость: {Capacity}");
            sb.AppendLine("seq | frame | time | квест | состояние | причина | логика | детали");
            foreach (var entry in Entries())
                sb.AppendLine(entry.ToExportLine());
            return sb.ToString();
        }
    }
}
#endif
