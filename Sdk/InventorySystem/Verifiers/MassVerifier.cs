#if USING_VORTEX_ITEMS
using System;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>Предел суммарной массы. Предмет без свойства массы весит ноль.</summary>
    [Serializable]
    public class MassVerifier : QuantityVerifier
    {
        protected override long Measure(ItemModel item) =>
            item.GetProperty<IMassProperty>()?.GetMass(item) ?? 0;
    }
}
#endif
