#if USING_VORTEX_ITEMS
using System;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Объём за единицу. Устроен симметрично массе: единица на количество из свойства пачки.
    /// </summary>
    [Serializable]
    public class VolumeProperty : ItemProperty, IVolumeProperty
    {
        [SerializeField, Min(0)] private int unitVolume;

        public long GetVolume(ItemModel owner)
        {
            var stack = owner.GetProperty<IStackProperty>();
            long count = stack?.Count ?? 1;
            return (long)unitVolume * count;
        }
    }
}
#endif
