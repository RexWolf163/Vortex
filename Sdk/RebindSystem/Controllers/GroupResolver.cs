using System;
using System.Collections.Generic;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Принадлежность биндингов группам устройств.
    ///
    /// Заводской биндинг: явная метка — поле <c>groups</c> биндинга в ассете, совпадающее с ключом группы
    /// (и группа подходит устройству); нет метки или она не совпала — первая по порядку конфига группа с
    /// подходящим устройством. Метки система только читает — они авторские.
    /// Добавленные системой биндинги меток не несут: их группа известна модели.
    /// </summary>
    internal static class GroupResolver
    {
        /// <summary>Устройство входит в группу — с учётом наследования layout'ов.</summary>
        internal static bool Matches(DeviceGroupSettings group, string deviceLayout)
        {
            if (group == null || string.IsNullOrEmpty(deviceLayout))
                return false;
            foreach (var layout in group.DeviceLayouts)
                if (ServiceRule.IsBasedOn(deviceLayout, layout))
                    return true;
            return false;
        }

        /// <summary>Группа заводского биндинга. <c>null</c> — устройство не входит ни в одну группу.</summary>
        internal static DeviceGroupSettings ResolveFactory(IReadOnlyList<DeviceGroupSettings> groups, string marks,
            string deviceLayout)
        {
            if (!string.IsNullOrEmpty(marks))
            {
                foreach (var raw in marks.Split(';'))
                {
                    var mark = raw.Trim();
                    if (mark.Length == 0)
                        continue;
                    foreach (var group in groups)
                        if (group != null && string.Equals(group.Key, mark, StringComparison.OrdinalIgnoreCase)
                                          && Matches(group, deviceLayout))
                            return group;
                }
            }

            foreach (var group in groups)
                if (Matches(group, deviceLayout))
                    return group;
            return null;
        }
    }
}
