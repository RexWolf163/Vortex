#if USING_VORTEX_ITEMS
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Назначение «пачка». Признак стекуемости предмета: наличие этого свойства плюс совпадение
    /// типа делают два предмета сливаемыми. Количество — игровое состояние, меняет его только
    /// контроллер пачек.
    /// </summary>
    public interface IStackProperty : IItemProperty
    {
        /// <summary>Максимум пачки — сколько единиц помещается в один экземпляр. Настройка.</summary>
        int Max { get; }

        /// <summary>Текущее количество в пачке.</summary>
        int Count { get; }
    }
}
#endif
