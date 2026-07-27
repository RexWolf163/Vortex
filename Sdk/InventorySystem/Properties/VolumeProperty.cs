#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
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
        [InfoBox("Объём за единицу")] [SerializeField, Min(0)]
        private int unitVolume;

        public long GetVolume(ItemModel owner)
        {
            var stack = owner.GetProperty<IStackProperty>();
            long count = stack?.Count ?? 1;
            return (long)unitVolume * count;
        }

        protected override string Label => $"Volume: {unitVolume}pts.";
    }
}
#endif