#if USING_VORTEX_ITEMS
using System;
using UnityEngine;

namespace Vortex.Sdk.InventorySystem.Model
{
    /// <summary>
    /// Строка стартового наполнения инвентаря — настройка, а не состояние. Идентификатор пресета
    /// и количество: количество кладётся в свойство пачки построенного предмета, прочее состояние
    /// берётся из настройки предмета целиком.
    /// </summary>
    [Serializable]
    public class StartupEntry
    {
        [SerializeField] private string presetGuid;
        [SerializeField, Min(1)] private int count = 1;

        /// <summary>Идентификатор пресета предмета.</summary>
        public string PresetGuid => presetGuid;

        /// <summary>Начальное количество в пачке. Для нестекуемого предмета имеет смысл только 1.</summary>
        public int Count => count;
    }
}
#endif
