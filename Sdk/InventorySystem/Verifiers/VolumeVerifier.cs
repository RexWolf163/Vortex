#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>Предел суммарного объёма. Предмет без свойства объёма занимает ноль.</summary>
    [Serializable, InfoBox("Предел суммарного объёма.")]
    public class VolumeVerifier : QuantityVerifier
    {
        protected override long Measure(ItemModel item) =>
            item.GetProperty<IVolumeProperty>()?.GetVolume(item) ?? 0;
    }
}
#endif
