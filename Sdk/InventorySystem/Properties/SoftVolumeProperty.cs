#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Мягкий объём контейнера: собственный плюс объём содержимого. Мешок, сеть, вьюк раздуваются от
    /// наполнения. Как и масса контейнера, единичен — на количество пачки не умножается; рекурсия по
    /// вложенным контейнерам получается сама.
    /// </summary>
    [Serializable]
    public class SoftVolumeProperty : ItemProperty, IVolumeProperty
    {
        [InfoBox("Мягкий объём контейнера: собственный плюс объём содержимого.")] [SerializeField, Min(0)]
        private int selfVolume;

        public long GetVolume(ItemModel owner)
        {
            long total = selfVolume;

            var container = owner.GetProperty<IContainerProperty>();
            if (container == null)
                return total;

            foreach (var inner in container.Inventory.Items)
            {
                var volume = inner.GetProperty<IVolumeProperty>();
                if (volume != null)
                    total += volume.GetVolume(inner);
            }

            return total;
        }
    }
}
#endif