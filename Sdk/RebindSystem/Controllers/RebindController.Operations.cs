using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Операции над слотами и группами (ТЗ 2.2–2.10). Каждая изменяющая операция — одно сохранение и одно
    /// событие. Операции не создают внутрикартовых конфликтов: они возможны только из загрузки или импорта.
    /// </summary>
    public sealed partial class RebindController
    {
        /// <summary>Накопитель изменений одной операции.</summary>
        private sealed class Scope
        {
            public readonly HashSet<BindSlot> Changed = new();
            public readonly HashSet<RebindCommand> Commands = new();
            public readonly HashSet<(string group, string signature)> Buckets = new();
        }

        /// <summary>Сохранить снимок. Реализуется в части снимка.</summary>
        partial void Persist();

        // ── Назначение ──────────────────────────────────────────────────────────────────────────────

        public RebindResult Assign(string bindKey, BindingValue value) => Place(bindKey, value, take: false);

        public RebindResult Take(string bindKey, BindingValue value) => Place(bindKey, value, take: true);

        private RebindResult Place(string bindKey, BindingValue value, bool take)
        {
            var reason = ResolveSlot(bindKey, out var command, out var slot);
            if (reason == RejectReason.None)
                reason = CheckCandidate(slot, value);
            if (reason != RejectReason.None)
                return RebindResult.Rejected(reason);

            var signature = Signature.Of(value, DistinguishSides);
            var intra = new List<ConflictInfo>();
            var donors = new List<BindSlot>();
            foreach (var other in Model.Index.Find(slot.Group, signature))
            {
                if (other.CommandId == slot.CommandId || other.Map != slot.Map || IsAllowedPair(slot.CommandId, other.CommandId))
                    continue;
                intra.Add(new ConflictInfo(signature, SlotConflict.IntraMap, other.BindKey, other.Map));
                donors.Add(other);
            }

            if (intra.Count > 0 && !take)
                return RebindResult.Rejected(RejectReason.IntraMapConflict, intra);

            foreach (var donorGroup in donors.GroupBy(d => d.CommandId))
                if (IsProtected(donorGroup.Key) && RemainingAfter(Model.Commands[donorGroup.Key], donorGroup) == 0)
                    return RebindResult.Rejected(RejectReason.ProtectedLastBinding, intra);

            var scope = new Scope();
            // Склейка: клавиша уже есть у этой команды в группе — переезжает в целевой слот.
            foreach (var same in command.GetSlots(slot.Group))
                if (!ReferenceEquals(same, slot) && same.Signature == signature)
                    SetValue(same, BindingValue.Empty, true, scope);

            SetValue(slot, value, true, scope);
            foreach (var donor in donors)
                SetValue(donor, BindingValue.Empty, true, scope);

            return Commit(scope, slot);
        }

        // ── Очистка ─────────────────────────────────────────────────────────────────────────────────

        public RebindResult Clear(string bindKey)
        {
            var reason = ResolveSlot(bindKey, out var command, out var slot);
            if (reason != RejectReason.None)
                return RebindResult.Rejected(reason);
            if (slot.Value.IsEmpty)
                return RebindResult.Applied(Array.Empty<string>(), Array.Empty<ConflictInfo>());
            if (IsProtected(command.Id) && RemainingAfter(command, new[] { slot }) == 0)
                return RebindResult.Rejected(RejectReason.ProtectedLastBinding);

            var scope = new Scope();
            SetValue(slot, BindingValue.Empty, true, scope);
            return Commit(scope, null);
        }

        // ── Обмен ───────────────────────────────────────────────────────────────────────────────────

        public RebindResult Swap(string bindKeyA, string bindKeyB)
        {
            RebindCommand commandB = null;
            BindSlot b = null;
            var reason = ResolveSlot(bindKeyA, out var commandA, out var a);
            if (reason == RejectReason.None)
                reason = ResolveSlot(bindKeyB, out commandB, out b);
            if (reason != RejectReason.None)
                return RebindResult.Rejected(reason);

            if (ReferenceEquals(a, b))
                return RebindResult.Applied(Array.Empty<string>(), Array.Empty<ConflictInfo>());

            var valueA = a.Value;
            var valueB = b.Value;

            var conflicts = new List<ConflictInfo>();
            reason = CheckPlacement(a, valueB, b, conflicts);
            if (reason == RejectReason.None)
                reason = CheckPlacement(b, valueA, a, conflicts);
            if (reason != RejectReason.None)
                return RebindResult.Rejected(reason, conflicts);

            // Перенос в пустой слот: команда-источник теряет клавишу — проверяем защиту.
            if (commandA != commandB)
            {
                if (valueB.IsEmpty && !valueA.IsEmpty && IsProtected(commandA.Id) && RemainingAfter(commandA, new[] { a }) == 0)
                    return RebindResult.Rejected(RejectReason.ProtectedLastBinding);
                if (valueA.IsEmpty && !valueB.IsEmpty && IsProtected(commandB.Id) && RemainingAfter(commandB, new[] { b }) == 0)
                    return RebindResult.Rejected(RejectReason.ProtectedLastBinding);
            }

            var scope = new Scope();
            GlueFor(commandA, a, valueB, b, scope);
            GlueFor(commandB, b, valueA, a, scope);
            SetValue(a, valueB, true, scope);
            SetValue(b, valueA, true, scope);
            return Commit(scope, a);
        }

        /// <summary>
        /// Проверка размещения значения в слот при обмене (п. 5 и 9 порядка проверки): устройство подходит
        /// группе, нет внутрикартового конфликта с чужими командами — без учёта участников обмена.
        /// </summary>
        private RejectReason CheckPlacement(BindSlot target, BindingValue value, BindSlot partner, List<ConflictInfo> conflicts)
        {
            if (value.IsEmpty)
                return RejectReason.None;
            if (!GroupResolver.Matches(Model.GroupsByKey[target.Group], ServiceRule.DeviceLayout(value.Trigger)))
                return RejectReason.DeviceNotInGroup;

            var signature = Signature.Of(value, DistinguishSides);
            var found = false;
            foreach (var other in Model.Index.Find(target.Group, signature))
            {
                if (ReferenceEquals(other, target) || ReferenceEquals(other, partner)
                                                   || other.CommandId == target.CommandId || other.Map != target.Map
                                                   || IsAllowedPair(target.CommandId, other.CommandId))
                    continue;
                conflicts.Add(new ConflictInfo(signature, SlotConflict.IntraMap, other.BindKey, other.Map));
                found = true;
            }

            return found ? RejectReason.IntraMapConflict : RejectReason.None;
        }

        /// <summary>Склейка при обмене: такая клавиша уже есть у команды в группе — прежний слот очищается.</summary>
        private void GlueFor(RebindCommand command, BindSlot target, BindingValue value, BindSlot partner, Scope scope)
        {
            if (value.IsEmpty)
                return;
            var signature = Signature.Of(value, DistinguishSides);
            foreach (var same in command.GetSlots(target.Group))
                if (!ReferenceEquals(same, target) && !ReferenceEquals(same, partner) && same.Signature == signature)
                    SetValue(same, BindingValue.Empty, true, scope);
        }

        // ── Сброс ───────────────────────────────────────────────────────────────────────────────────

        public void ResetCommand(string commandId)
        {
            if (Model == null || commandId == null || !Model.Commands.TryGetValue(commandId, out var command))
                return;
            var scope = new Scope();
            ResetInto(command, scope);
            // ТЗ 2.8: сброс к X не нормализуется — как и загрузка.
            Commit(scope, null, normalize: false);
        }

        public void ResetMap(string map)
        {
            // null для GetCommands — «все карты»; сброс всего — только явным ResetAll.
            if (Model == null || map == null)
                return;
            var scope = new Scope();
            foreach (var command in Model.GetCommands(map))
                ResetInto(command, scope);
            CommitRebuilt();
        }

        /// <summary>Сброс всех изменений до заводского состояния, включая активность групп.</summary>
        public void ResetAll()
        {
            if (Model == null)
                return;
            var scope = new Scope();
            foreach (var command in Model.Commands.Values)
                ResetInto(command, scope);
            ResetActivityToDefault();
            CommitRebuilt();
            RebindBus.RaiseGroupsChanged();
        }

        /// <summary>Заводские значения, признаки явного назначения сняты, добавленные слоты опустошены.</summary>
        private void ResetInto(RebindCommand command, Scope scope)
        {
            foreach (var slot in command.AllSlots)
                SetValue(slot, slot.Default ?? BindingValue.Empty, false, scope);
        }

        // ── Раскладки ───────────────────────────────────────────────────────────────────────────────

        public void SetGroupActive(string groupKey, bool active)
        {
            if (Model == null || groupKey == null || !Model.GroupsByKey.TryGetValue(groupKey, out var group))
                return;

            var affected = new HashSet<string>();
            if (active)
            {
                if (Model.ActiveGroups.Add(groupKey))
                    affected.Add(groupKey);
                if (!string.IsNullOrEmpty(group.SwitchSet))
                    foreach (var other in Model.Groups)
                        if (other.Key != groupKey && other.SwitchSet == group.SwitchSet && Model.ActiveGroups.Remove(other.Key))
                            affected.Add(other.Key);
            }
            else if (Model.ActiveGroups.Remove(groupKey))
            {
                affected.Add(groupKey);
            }

            if (affected.Count == 0)
                return;

            // Индекс не меняется: конфликты считаются и в неактивных раскладках.
            foreach (var slot in Model.Slots.Values)
                if (affected.Contains(slot.Group))
                    AssetWriter.Apply(Model.Commands[slot.CommandId], slot, Model.IsGroupActive(slot.Group));

            Persist();
            RebindBus.RaiseGroupsChanged();
        }

        // ── Чтение ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>Первая кнопка команды в группе. <c>null</c> — нет.</summary>
        public BindSlot GetFirst(string commandId, string groupKey) =>
            groupKey == null ? null : GetAll(commandId, groupKey).FirstOrDefault();

        /// <summary>Кнопки команды: в указанной группе или во всех (порядок групп конфига, затем слотов).</summary>
        public IReadOnlyList<BindSlot> GetAll(string commandId, string groupKey = null)
        {
            if (Model == null || commandId == null || !Model.Commands.TryGetValue(commandId, out var command)
                || !command.Serviced)
                return Array.Empty<BindSlot>();

            var slots = groupKey == null
                ? Model.Groups.SelectMany(g => command.GetSlots(g.Key))
                : command.GetSlots(groupKey);
            return slots.Where(s => !s.Value.IsEmpty).ToArray();
        }

        // ── Общее ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Разбор адреса и поиск слота (п. 1–3 порядка проверки).</summary>
        private RejectReason ResolveSlot(string bindKey, out RebindCommand command, out BindSlot slot)
        {
            command = null;
            slot = null;
            if (Model == null || !BindSlot.TryParseKey(bindKey, out var commandId, out var groupKey, out var index))
                return RejectReason.UnknownSlot;
            if (!Model.Commands.TryGetValue(commandId, out command))
                return RejectReason.UnknownCommand;
            if (!command.Serviced)
                return RejectReason.SkippedCommand;
            if (!Model.GroupsByKey.TryGetValue(groupKey, out var group))
                return RejectReason.UnknownGroup;

            var list = command.GetSlots(groupKey);
            if (index < 0 || index >= group.Slots || index >= list.Count)
                return RejectReason.UnknownSlot;

            slot = list[index];
            return RejectReason.None;
        }

        /// <summary>Кандидат: обслуживаемый, устройство из группы слота, не запрещён (п. 4–7).</summary>
        private RejectReason CheckCandidate(BindSlot slot, BindingValue value)
        {
            var reason = ServiceRule.ValidateCandidate(value);
            if (reason != RejectReason.None)
                return reason;
            if (!GroupResolver.Matches(Model.GroupsByKey[slot.Group], ServiceRule.DeviceLayout(value.Trigger)))
                return RejectReason.DeviceNotInGroup;
            if (ForbiddenMatcher.IsForbidden(value, Settings?.Forbidden))
                return RejectReason.ForbiddenKey;
            return RejectReason.None;
        }

        /// <summary>
        /// Сколько клавиш останется у команды во всех группах, если снять указанные слоты. Слоты сверх X группы не
        /// в счёт — их опустошит нормализация той же операции.
        /// </summary>
        private int RemainingAfter(RebindCommand command, IEnumerable<BindSlot> removed)
        {
            var excluded = new HashSet<BindSlot>(removed);
            return command.AllSlots.Count(s => !s.Value.IsEmpty && !excluded.Contains(s)
                                                                && s.Index < Model.GroupsByKey[s.Group].Slots);
        }

        /// <summary>Сменить значение слота с поддержкой индекса.</summary>
        private void SetValue(BindSlot slot, BindingValue value, bool isExplicit, Scope scope)
        {
            var valueChanged = !slot.Value.Equals(value);
            if (!valueChanged && slot.IsExplicit == isExplicit)
                return;

            if (valueChanged)
            {
                scope.Buckets.Add((slot.Group, slot.Signature));
                Model.Index.Remove(slot);
                slot.Value = value;
                slot.Signature = Signature.Of(value, DistinguishSides);
                Model.Index.Add(slot);
                scope.Buckets.Add((slot.Group, slot.Signature));
            }

            slot.IsExplicit = isExplicit;
            scope.Changed.Add(slot);
            scope.Commands.Add(Model.Commands[slot.CommandId]);
        }

        /// <summary>
        /// Нормализация групп затронутых команд (Inv-7): склеить дубли (остаётся первый по порядку слотов),
        /// опустошить слоты с индексом ≥ X группы.
        /// </summary>
        private void Normalize(Scope scope)
        {
            foreach (var command in scope.Commands.ToArray())
            foreach (var group in Model.Groups)
            {
                var seen = new HashSet<string>();
                var list = command.GetSlots(group.Key);
                for (var i = 0; i < list.Count; i++)
                {
                    var slot = list[i];
                    if (slot.Value.IsEmpty)
                        continue;
                    if (i >= group.Slots || !seen.Add(slot.Signature))
                        SetValue(slot, BindingValue.Empty, true, scope);
                }
            }
        }

        /// <summary>
        /// Завершение операции над слотами: нормализация (кроме сброса), конфликты, ассет, сохранение, событие.
        /// </summary>
        private RebindResult Commit(Scope scope, BindSlot target, bool normalize = true)
        {
            if (scope.Changed.Count == 0)
                return RebindResult.Applied(Array.Empty<string>(), Array.Empty<ConflictInfo>());

            if (normalize)
                Normalize(scope);
            var events = new HashSet<BindSlot>(scope.Changed);
            foreach (var (group, signature) in scope.Buckets)
            foreach (var slot in Model.Index.Find(group, signature))
            {
                var level = ComputeConflict(slot, null);
                if (level != slot.Conflict)
                    events.Add(slot);
                slot.Conflict = level;
            }

            foreach (var slot in scope.Changed)
            {
                if (slot.Value.IsEmpty)
                    slot.Conflict = SlotConflict.None;
                AssetWriter.Apply(Model.Commands[slot.CommandId], slot, Model.IsGroupActive(slot.Group));
            }

            Persist();

            var keys = events.Select(s => s.BindKey).ToArray();
            RebindBus.RaiseSlotsChanged(keys);

            var crossMap = new List<ConflictInfo>();
            if (target != null)
                ComputeConflict(target, crossMap);
            return RebindResult.Applied(keys, crossMap.Where(c => c.Level == SlotConflict.CrossMap).ToArray());
        }

        /// <summary>
        /// Завершение массового сброса: полная пересборка индекса и ассета (включая активность групп), сохранение,
        /// «пересобрано всё».
        /// </summary>
        private void CommitRebuilt()
        {
            RebuildIndex();
            ApplyAllToAsset();
            Persist();
            RebindBus.RaiseRebuilt();
        }
    }
}
