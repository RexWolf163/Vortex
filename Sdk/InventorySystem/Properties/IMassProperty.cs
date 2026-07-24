#if USING_VORTEX_ITEMS
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Назначение «масса». Величина возвращается методом, принимающим предмет-владельца: она может
    /// зависеть от других свойств того же предмета — прежде всего от пачки (масса единицы на
    /// количество). Ссылку на владельца свойство не хранит: она замкнула бы цикл в сохранении.
    /// </summary>
    public interface IMassProperty : IItemProperty
    {
        /// <summary>Суммарная масса предмета. Расширенное целое: сумма единиц на количество пачки.</summary>
        long GetMass(ItemModel owner);
    }
}
#endif
