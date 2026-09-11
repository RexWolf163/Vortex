using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Команда — проекция экшена Input System со слотами по группам устройств. Пропускаемая команда
    /// (список конфига) слотов не имеет и системой не обслуживается.
    /// </summary>
    public sealed class RebindCommand
    {
        internal RebindCommand(string id, string map, InputAction action, bool serviced)
        {
            Id = id;
            Map = map;
            Action = action;
            Serviced = serviced;
        }

        /// <summary>Id команды: «Карта/Экшен».</summary>
        public string Id { get; }

        public string Map { get; }

        /// <summary>Команда обслуживается системой (не в списке пропускаемых).</summary>
        public bool Serviced { get; }

        internal InputAction Action { get; }

        /// <summary>Слоты по ключу группы.</summary>
        internal Dictionary<string, List<BindSlot>> Groups { get; } = new();

        /// <summary>Слоты группы в порядке позиций. Неизвестная группа — пустой список.</summary>
        public IReadOnlyList<BindSlot> GetSlots(string groupKey) =>
            groupKey != null && Groups.TryGetValue(groupKey, out var list) ? list : Array.Empty<BindSlot>();

        /// <summary>Все слоты команды во всех группах.</summary>
        public IEnumerable<BindSlot> AllSlots => Groups.Values.SelectMany(list => list);
    }
}
