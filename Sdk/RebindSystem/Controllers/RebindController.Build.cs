using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>Построение модели из ассета Input System и конфига.</summary>
    public sealed partial class RebindController
    {
        private bool DistinguishSides => Settings != null && Settings.DistinguishModifierSides;

        /// <summary>
        /// Собрать модель с нуля: чистый лист ассета, команды, заводские слоты по группам, пустые слоты до X,
        /// активность групп по умолчанию. Индекс занятости и ассет пересобирает вызывающий — после наложения снимка.
        /// </summary>
        private void BuildModel()
        {
            var groups = Settings != null ? Settings.Groups : Array.Empty<DeviceGroupSettings>();
            Model = new RebindModel(groups);
            ResetActivityToDefault();

            var asset = InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("[RebindController] Не назначен проектный ассет Input System (InputSystem.actions).");
                return;
            }

            AssetWriter.CleanSlate(asset);

            var skipped = new HashSet<string>(Settings != null ? Settings.SkippedCommands : Array.Empty<string>());
            foreach (var map in asset.actionMaps)
            foreach (var action in map.actions)
            {
                var id = $"{map.name}/{action.name}";
                if (Model.Commands.ContainsKey(id))
                    continue;

                var command = new RebindCommand(id, map.name, action, !skipped.Contains(id));
                Model.Commands[id] = command;
                foreach (var group in Model.Groups)
                    command.Groups[group.Key] = new List<BindSlot>();

                // Пропускаемая команда невидима для системы целиком — слотов у неё нет.
                if (!command.Serviced)
                    continue;

                ReadFactorySlots(command, action);
                foreach (var group in Model.Groups)
                {
                    var list = command.Groups[group.Key];
                    // Заводские биндинги живут в ассете ввода — OnValidate конфига их правку не видит.
                    if (list.Count > group.Slots)
                        Debug.LogError($"[RebindController] У команды «{id}» в группе «{group.Key}» заводских " +
                                       $"биндингов {list.Count} — больше числа слотов группы ({group.Slots}).");
                    while (list.Count < group.Slots)
                        list.Add(new BindSlot(id, map.name, group.Key, list.Count));
                    foreach (var slot in list)
                        Model.Slots[slot.BindKey] = slot;
                }
            }
        }

        /// <summary>
        /// Заводские обслуживаемые биндинги экшена — в слоты своих групп, в порядке ассета. Биндинг, чьё
        /// устройство не входит ни в одну группу, надсистемный.
        /// </summary>
        private void ReadFactorySlots(RebindCommand command, InputAction action)
        {
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var serviced = ServiceRule.TryReadServiced(action, i, out var value, out var parts);
                if (serviced)
                {
                    var binding = bindings[i];
                    var group = GroupResolver.ResolveFactory(Model.Groups, binding.groups,
                        ServiceRule.DeviceLayout(value.Trigger));
                    if (group != null)
                    {
                        var list = command.Groups[group.Key];
                        list.Add(new BindSlot(command.Id, command.Map, group.Key, list.Count)
                        {
                            Default = value,
                            Value = value,
                            FactoryBindingId = binding.id
                        });
                    }
                }

                i += parts;
            }
        }

        private void ResetActivityToDefault()
        {
            Model.ActiveGroups.Clear();
            Model.ActiveGroups.UnionWith(DefaultActiveGroups());
        }

        /// <summary>Активность по умолчанию: первая по порядку группа каждого набора и все самостоятельные.</summary>
        private HashSet<string> DefaultActiveGroups()
        {
            var result = new HashSet<string>();
            var sets = new HashSet<string>();
            foreach (var group in Model.Groups)
                if (string.IsNullOrEmpty(group.SwitchSet) || sets.Add(group.SwitchSet))
                    result.Add(group.Key);
            return result;
        }

        /// <summary>
        /// Состояние отличается от заводского: есть переопределённый, добавленный или снятый слот либо активность
        /// групп не по умолчанию. Слот, назначенный явно, но совпадающий с заводским, отличием не считается.
        /// </summary>
        public bool HasChanges()
        {
            if (Model == null)
                return false;
            foreach (var slot in Model.Slots.Values)
                if (slot.Origin is SlotOrigin.Overridden or SlotOrigin.Added or SlotOrigin.Cleared)
                    return true;
            return !Model.ActiveGroups.SetEquals(DefaultActiveGroups());
        }

        /// <summary>Пересобрать индекс занятости и состояния конфликта всех слотов.</summary>
        private void RebuildIndex()
        {
            Model.Index.Clear();
            foreach (var slot in Model.Slots.Values)
            {
                slot.Signature = Signature.Of(slot.Value, DistinguishSides);
                Model.Index.Add(slot);
            }

            foreach (var slot in Model.Slots.Values)
                slot.Conflict = ComputeConflict(slot, null);
        }

        /// <summary>Привести ассет к состоянию всех слотов (с учётом активности групп).</summary>
        private void ApplyAllToAsset()
        {
            foreach (var slot in Model.Slots.Values)
                AssetWriter.Apply(Model.Commands[slot.CommandId], slot, Model.IsGroupActive(slot.Group));
        }

        /// <summary>
        /// Уровень конфликта слота; при переданном списке — сведения о каждом пересечении. Конфликт считается
        /// только внутри группы слота.
        /// </summary>
        private SlotConflict ComputeConflict(BindSlot slot, List<ConflictInfo> infos)
        {
            if (slot.Signature == null)
                return SlotConflict.None;

            var level = SlotConflict.None;
            foreach (var other in Model.Index.Find(slot.Group, slot.Signature))
            {
                if (ReferenceEquals(other, slot) || other.CommandId == slot.CommandId)
                    continue;

                var current = other.Map != slot.Map
                    ? SlotConflict.CrossMap
                    : IsAllowedPair(slot.CommandId, other.CommandId)
                        ? SlotConflict.IntraMapAllowed
                        : SlotConflict.IntraMap;

                infos?.Add(new ConflictInfo(slot.Signature, current, other.BindKey, other.Map));
                if (current > level)
                    level = current;
            }

            return level;
        }

        private bool IsAllowedPair(string a, string b)
        {
            if (Settings == null)
                return false;
            foreach (var pair in Settings.AllowedPairs)
                if (pair != null && pair.Matches(a, b))
                    return true;
            return false;
        }

        private bool IsProtected(string commandId)
        {
            if (Settings == null)
                return false;
            foreach (var id in Settings.ProtectedCommands)
                if (id == commandId)
                    return true;
            return false;
        }
    }
}
