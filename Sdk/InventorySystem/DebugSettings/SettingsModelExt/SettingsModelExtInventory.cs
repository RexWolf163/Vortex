#if USING_VORTEX_ITEMS
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        /// <summary>Отладочный флаг пакета инвентарей. Подчинён глобальному отладочному режиму.</summary>
        public bool InventoryDebugMode { get; private set; }
    }
}
#endif
