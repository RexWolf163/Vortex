#if USING_VORTEX_ITEMS
using System;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.InventorySystem.Model
{
    /// <summary>
    /// Сериализуемое представление одного предмета в составе инвентаря: чем он был и в каком
    /// состоянии. Живёт только в момент сохранения и загрузки — между ними есть предметы, а не записи.
    /// Состояние строкой: предмет сам решает, что и как сохранять, инвентарь его устройства не знает.
    /// </summary>
    [Serializable, POCO]
    public class SavedItem
    {
        /// <summary>Идентификатор пресета — по нему шина предметов берёт форму.</summary>
        public string Preset { get; set; }

        /// <summary>Состояние предмета — то, что он сам отдал на сохранении.</summary>
        public string State { get; set; }
    }
}
#endif
