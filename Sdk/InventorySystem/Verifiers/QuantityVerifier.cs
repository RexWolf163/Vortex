#if USING_VORTEX_ITEMS
using System;
using UnityEngine;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Bus;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Обобщённое правило «сумма измеряемой величины против предела». Повторяющаяся часть массового
    /// и объёмного правил вынесена сюда: наследник добавляет только то, как измерить один предмет.
    ///
    /// Сумма кешируется по общей оси версии. Экземпляр правила свой у каждого инвентаря (приезжает
    /// глубоким копированием из настройки), поэтому кеш на экземпляре безопасен. Пока ось не
    /// сдвинулась — а сдвигает её любое изменение состава или значения внутри предмета, — сумма не
    /// пересчитывается.
    ///
    /// Предел и сумма в расширенном целом: десять тысяч предметов с большими единичными величинами
    /// выходят за обычное целое, а переполнение здесь означало бы отрицательную занятость с
    /// бесконечной вместимостью.
    /// </summary>
    [Serializable]
    public abstract class QuantityVerifier : InventoryVerifier
    {
        [SerializeField, Min(0)] private long max;

        private long _cachedCurrent;
        private long _cachedAxis = -1;

        /// <summary>Измеренная величина одного предмета — включая его пачку и, у контейнеров, содержимое.</summary>
        protected abstract long Measure(ItemModel item);

        public override long GetMax(Inventory inventory) => max;

        public override long GetCurrent(Inventory inventory)
        {
            var axis = ItemsBus.Version;
            if (axis == _cachedAxis)
                return _cachedCurrent;

            long sum = 0;
            foreach (var item in inventory.Items)
                sum += Measure(item);

            _cachedCurrent = sum;
            _cachedAxis = axis;
            return sum;
        }

        public override bool CanPlace(Inventory inventory, ItemModel item) =>
            GetCurrent(inventory) + Measure(item) <= max;
    }
}
#endif
