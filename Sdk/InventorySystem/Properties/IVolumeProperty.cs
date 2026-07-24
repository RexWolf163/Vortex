#if USING_VORTEX_ITEMS
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Назначение «объём». Устроено симметрично массе: величина возвращается методом с предметом-
    /// владельцем, потому что зависит от пачки, а у контейнеров — ещё и от содержимого.
    /// </summary>
    public interface IVolumeProperty : IItemProperty
    {
        /// <summary>Суммарный объём предмета. Расширенное целое.</summary>
        long GetVolume(ItemModel owner);
    }
}
#endif
