using System;
using System.Collections.Generic;

namespace Vortex.Sdk.RebindSystem.Model
{
    public enum RebindStatus
    {
        Applied,
        Rejected,

        /// <summary>Операция прервана: игрок отменил перехват, окно потеряло фокус, импорт до загрузки.</summary>
        Cancelled,

        /// <summary>
        /// Операция ничего не изменила: запрошенное состояние уже выполнено (та же клавиша в тот же слот,
        /// очистка пустого слота, обмен слота с самим собой, сброс уже заводского состояния). Снимок не
        /// пишется, события не поднимаются. Новые члены — только в конец: значения используются как номера.
        /// </summary>
        Unchanged
    }

    public enum RejectReason
    {
        None,
        IntraMapConflict,
        ForbiddenKey,
        ProtectedLastBinding,
        UnknownCommand,
        UnknownGroup,

        /// <summary>Адрес не разобран или индекс слота ≥ X группы.</summary>
        UnknownSlot,

        DeviceNotInGroup,
        SkippedCommand,
        TooManyModifiers,
        AmbiguousModifiers,

        /// <summary>Не обслуживаемый контрол, модификатор не Shift/Ctrl/Alt или комбинация с триггером не клавиатуры/мыши.</summary>
        InvalidTrigger
    }

    /// <summary>Сведения о пересечении слота с другим.</summary>
    public readonly struct ConflictInfo
    {
        public ConflictInfo(string signature, SlotConflict level, string otherBindKey, string map)
        {
            Signature = signature;
            Level = level;
            OtherBindKey = otherBindKey;
            Map = map;
        }

        public string Signature { get; }

        public SlotConflict Level { get; }

        /// <summary>Слот другой стороны пересечения.</summary>
        public string OtherBindKey { get; }

        /// <summary>Карта другой стороны.</summary>
        public string Map { get; }
    }

    /// <summary>
    /// Ответ изменяющей операции и клапана. Неизменяем. При <see cref="RebindStatus.Rejected"/> в
    /// <see cref="Conflicts"/> — внутрикартовые конфликты, при <see cref="RebindStatus.Applied"/> —
    /// межкартовые (<see cref="SlotConflict.CrossMap"/> и <see cref="SlotConflict.Common"/>, для подсветки).
    /// </summary>
    public sealed class RebindResult
    {
        private RebindResult(RebindStatus status, RejectReason reason, IReadOnlyList<ConflictInfo> conflicts,
            IReadOnlyList<string> changedSlots)
        {
            Status = status;
            Reason = reason;
            Conflicts = conflicts ?? Array.Empty<ConflictInfo>();
            ChangedSlots = changedSlots ?? Array.Empty<string>();
        }

        public RebindStatus Status { get; }

        public RejectReason Reason { get; }

        public IReadOnlyList<ConflictInfo> Conflicts { get; }

        /// <summary>Все слоты, изменённые операцией (значение или состояние конфликта).</summary>
        public IReadOnlyList<string> ChangedSlots { get; }

        internal static RebindResult Applied(IReadOnlyList<string> changedSlots, IReadOnlyList<ConflictInfo> crossMap) =>
            new(RebindStatus.Applied, RejectReason.None, crossMap, changedSlots);

        internal static RebindResult Rejected(RejectReason reason, IReadOnlyList<ConflictInfo> conflicts = null) =>
            new(RebindStatus.Rejected, reason, conflicts, null);

        internal static RebindResult Cancelled() => new(RebindStatus.Cancelled, RejectReason.None, null, null);

        internal static RebindResult Unchanged() => new(RebindStatus.Unchanged, RejectReason.None, null, null);
    }
}
