#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Масса контейнера: собственный вес пустого контейнера плюс масса содержимого. Отдельный класс
    /// на том же назначении, что и обычная масса, — двух свойств на одном назначении в предмете не
    /// бывает, поэтому сумка несёт либо это свойство, либо простую массу, но не оба.
    ///
    /// Контейнер — единичный предмет, поэтому на количество пачки масса не умножается: осмысленной
    /// пачки одинаковых контейнеров с одинаковым содержимым не существует. Рекурсия по вложенным
    /// контейнерам получается сама — масса вложенного предмета спрашивается тем же назначением.
    /// </summary>
    [Serializable]
    public class ContainerMassProperty : ItemProperty, IMassProperty
    {
        [InfoBox("Масса контейнера: собственный вес пустого контейнера плюс масса содержимого")]
        [SerializeField, Min(0)]
        private int selfMass;

        public long GetMass(ItemModel owner)
        {
            long total = selfMass;

            var container = owner.GetProperty<IContainerProperty>();
            if (container == null)
                return total;

            foreach (var inner in container.Inventory.Items)
            {
                var mass = inner.GetProperty<IMassProperty>();
                if (mass != null)
                    total += mass.GetMass(inner);
            }

            return total;
        }
    }
}
#endif