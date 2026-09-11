using System;
using System.Collections.Generic;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Индекс занятости: «(группа, сигнатура) → слоты во всех картах». Производные данные — строятся из
    /// слотов, не хранятся. Даёт проверку кандидата и состояние конфликта без обхода всех биндингов:
    /// выборка — только слоты с той же клавишей в той же группе, дальше фильтр по карте.
    /// Конфликты между группами не считаются: непересекающиеся группы не совпадают по сигнатуре,
    /// пересекающиеся — альтернативные раскладки.
    /// </summary>
    internal sealed class OccupancyIndex
    {
        private readonly Dictionary<(string group, string signature), List<BindSlot>> _slots = new();

        internal void Clear() => _slots.Clear();

        /// <summary>Внести слот по его текущей сигнатуре. Пустой слот не индексируется.</summary>
        internal void Add(BindSlot slot)
        {
            if (slot.Signature == null)
                return;

            var key = (slot.Group, slot.Signature);
            if (!_slots.TryGetValue(key, out var list))
                _slots[key] = list = new List<BindSlot>();
            if (!list.Contains(slot))
                list.Add(slot);
        }

        /// <summary>Убрать слот по его текущей сигнатуре — вызывать до смены значения.</summary>
        internal void Remove(BindSlot slot)
        {
            if (slot.Signature == null)
                return;

            var key = (slot.Group, slot.Signature);
            if (!_slots.TryGetValue(key, out var list))
                return;
            list.Remove(slot);
            if (list.Count == 0)
                _slots.Remove(key);
        }

        /// <summary>Слоты группы с этой сигнатурой во всех картах.</summary>
        internal IReadOnlyList<BindSlot> Find(string group, string signature)
        {
            if (group == null || signature == null)
                return Array.Empty<BindSlot>();
            return _slots.TryGetValue((group, signature), out var list) ? list : Array.Empty<BindSlot>();
        }
    }
}
