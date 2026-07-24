#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Фильтр «запрещено свойство такого назначения» — зеркало <see cref="RequirePropertyVerifier{T}"/>.
    /// Проект закрывает параметр конкретным интерфейсом:
    /// <code>
    /// [Serializable] public class ForbidContainer : ForbidPropertyVerifier&lt;IContainerProperty&gt; { }
    /// </code>
    /// </summary>
    [Serializable, InfoBox("Запрещено свойство указанного назначения.")]
    public abstract class ForbidPropertyVerifier<T> : InventoryVerifier where T : class, IItemProperty
    {
        public override bool CanPlace(Inventory inventory, ItemModel item) => !item.HasProperty<T>();

        public override long GetMax(Inventory inventory) => 0;
        public override long GetCurrent(Inventory inventory) => 0;
    }
}
#endif
