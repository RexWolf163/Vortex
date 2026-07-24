#if USING_VORTEX_ITEMS
using System;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Масса за единицу. Суммарная масса — единица на количество из свойства пачки того же предмета;
    /// нет пачки — количество считается за единицу. Настроечное поле, сохраняемого состояния нет.
    /// </summary>
    [Serializable]
    public class MassProperty : ItemProperty, IMassProperty
    {
        [SerializeField, Min(0)] private int unitMass;

        public long GetMass(ItemModel owner)
        {
            var stack = owner.GetProperty<IStackProperty>();
            long count = stack?.Count ?? 1;
            return (long)unitMass * count;
        }
    }
}
#endif
