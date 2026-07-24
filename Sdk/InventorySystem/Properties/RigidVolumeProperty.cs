#if USING_VORTEX_ITEMS
using System;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Жёсткий объём контейнера: не зависит от наполнения. Рюкзак, ящик, сейф занимают своё место
    /// пустыми и полными одинаково. Стопка жёстких коробок умножает объём на количество пачки.
    /// </summary>
    [Serializable]
    public class RigidVolumeProperty : ItemProperty, IVolumeProperty
    {
        [SerializeField, Min(0)] private int selfVolume;

        public long GetVolume(ItemModel owner)
        {
            var stack = owner.GetProperty<IStackProperty>();
            long count = stack?.Count ?? 1;
            return (long)selfVolume * count;
        }
    }
}
#endif
