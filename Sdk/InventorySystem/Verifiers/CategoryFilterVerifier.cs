#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;
using Vortex.Unity.ExtensibleEnumSystem.Attributes;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Фильтр по категории предмета. Набор категорий плюс признак направления: белый список пускает
    /// только перечисленные, чёрный — всех, кроме перечисленных. Одним классом покрываются оба
    /// сундука — «только оружие» и «что угодно, кроме мусора».
    ///
    /// Предмет без заданной категории получает <c>Unknown</c>, которого в белом списке обычно нет, —
    /// значит строгий фильтр отсекает и ненастроенные предметы. Это верно, но при отладке об этом
    /// стоит помнить.
    /// </summary>
    [Serializable, InfoBox("Фильтр по категории предмета: белый или чёрный список.")]
    public class CategoryFilterVerifier : InventoryVerifier
    {
        [SerializeField] private bool whitelist = true;

        [SerializeField, ExtEnumKey(typeof(ItemCategory))]
        private List<string> categories = new();

        public override bool CanPlace(Inventory inventory, ItemModel item)
        {
            var inSet = categories.Contains(item.Category.Key);
            return whitelist ? inSet : !inSet;
        }

        public override long GetMax(Inventory inventory) => 0;
        public override long GetCurrent(Inventory inventory) => 0;
    }
}
#endif
