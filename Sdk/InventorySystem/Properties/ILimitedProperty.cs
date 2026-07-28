#if USING_VORTEX_ITEMS
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Назначение «лимит в инвентаре»: предмет объявляет, сколько его единиц суммарно допускается в
    /// одном инвентаре. Число, и только: суммирование по стекам и сама проверка — забота
    /// <see cref="Verifiers.LimitedVerifier"/>, поэтому свойство про пачки ничего не знает.
    /// </summary>
    public interface ILimitedProperty : IItemProperty
    {
        /// <summary>Максимум единиц этого предмета в инвентаре суммарно (по всем пачкам).</summary>
        int Limit { get; }
    }
}
#endif
