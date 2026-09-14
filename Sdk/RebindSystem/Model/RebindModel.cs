using System.Collections.Generic;
using System.Linq;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Модель состояния системы переназначения: команды и их слоты по группам устройств, активность групп,
    /// индекс занятости. Производная от ассета Input System, конфига и снимка; сама по себе не сохраняется.
    /// Снаружи — только чтение; изменения делает контроллер системы.
    /// </summary>
    public sealed class RebindModel
    {
        internal RebindModel(IReadOnlyList<DeviceGroupSettings> groups)
        {
            if (groups == null)
                return;
            foreach (var group in groups)
            {
                if (group == null || string.IsNullOrEmpty(group.Key) || GroupsByKey.ContainsKey(group.Key))
                    continue;
                GroupsByKey[group.Key] = group;
                OrderedGroups.Add(group);
            }
        }

        internal Dictionary<string, RebindCommand> Commands { get; } = new();

        /// <summary>Все слоты по адресу.</summary>
        internal Dictionary<string, BindSlot> Slots { get; } = new();

        internal Dictionary<string, DeviceGroupSettings> GroupsByKey { get; } = new();

        internal List<DeviceGroupSettings> OrderedGroups { get; } = new();

        internal HashSet<string> ActiveGroups { get; } = new();

        internal OccupancyIndex Index { get; } = new();

        /// <summary>Группы устройств в порядке конфига.</summary>
        public IReadOnlyList<DeviceGroupSettings> Groups => OrderedGroups;

        /// <summary>Команда по id «Карта/Экшен». <c>null</c> — нет такой.</summary>
        public RebindCommand GetCommand(string id) =>
            id != null && Commands.TryGetValue(id, out var command) ? command : null;

        /// <summary>Команды карты; без аргумента — все.</summary>
        public IEnumerable<RebindCommand> GetCommands(string map = null) =>
            map == null ? Commands.Values : Commands.Values.Where(c => c.Map == map);

        /// <summary>Слот по адресу. <c>null</c> — нет такого.</summary>
        public BindSlot GetSlot(string bindKey) =>
            bindKey != null && Slots.TryGetValue(bindKey, out var slot) ? slot : null;

        public bool IsGroupActive(string groupKey) => groupKey != null && ActiveGroups.Contains(groupKey);

        public IReadOnlyCollection<string> GetActiveGroups() => ActiveGroups;
    }
}