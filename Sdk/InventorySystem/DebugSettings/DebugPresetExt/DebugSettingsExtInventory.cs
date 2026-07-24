#if USING_VORTEX_ITEMS
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [SerializeField] [ToggleButton(isSingleButton: true)]
        [Tooltip("Нагружает производительность: сравнение состояний предметов при стекинге через " +
                 "сериализацию и обход всех живых инвентарей на дубликаты после новой игры и загрузки.")]
        private bool inventoryDebugMode;

        /// <summary>Флаг пакета инвентарей, подчинённый глобальному отладочному режиму.</summary>
        public bool InventoryDebugMode => DebugMode && inventoryDebugMode;
    }
}
#endif
