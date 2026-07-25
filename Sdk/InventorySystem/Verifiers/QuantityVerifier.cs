#if USING_VORTEX_ITEMS
using System;
using UnityEngine;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Обобщённое правило «сумма измеряемой величины против предела». Повторяющаяся часть массового
    /// и объёмного правил вынесена сюда: наследник добавляет только то, как измерить один предмет.
    ///
    /// Сумма считается по требованию, без кеша: <see cref="GetCurrent"/> перебирает состав и всегда
    /// возвращает текущую величину — включая распад значения внутри предмета, который поймать иначе
    /// нельзя. Проверка идёт при добавлении, не покадрово, а перебор реального инвентаря дёшев;
    /// целевое кеширование (event-driven сумма) вводится только если профиль покажет проблему.
    ///
    /// Предел и сумма в расширенном целом: десять тысяч предметов с большими единичными величинами
    /// выходят за обычное целое, а переполнение здесь означало бы отрицательную занятость с
    /// бесконечной вместимостью.
    /// </summary>
    [Serializable]
    public abstract class QuantityVerifier : InventoryVerifier
    {
        [SerializeField, Min(0)] private long max;

        /// <summary>Измеренная величина одного предмета — включая его пачку и, у контейнеров, содержимое.</summary>
        protected abstract long Measure(ItemModel item);

        public override long GetMax(Inventory inventory) => max;

        public override long GetCurrent(Inventory inventory)
        {
            long sum = 0;
            foreach (var item in inventory.Items)
                sum += Measure(item);
            return sum;
        }

        public override bool CanPlace(Inventory inventory, ItemModel item) =>
            GetCurrent(inventory) + Measure(item) <= max;
    }
}
#endif
