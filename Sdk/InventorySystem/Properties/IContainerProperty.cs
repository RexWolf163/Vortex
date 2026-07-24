#if USING_VORTEX_ITEMS
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Назначение «контейнер». Предмет несёт собственный инвентарь — сумка внутри сумки. Масса и
    /// объём такого предмета считаются вместе с содержимым отдельными свойствами
    /// (см. <see cref="ContainerMassProperty"/>, <see cref="SoftVolumeProperty"/>).
    /// </summary>
    public interface IContainerProperty : IItemProperty
    {
        /// <summary>Вложенный инвентарь. Никогда не <c>null</c> у построенного свойства.</summary>
        Inventory Inventory { get; }
    }
}
#endif
