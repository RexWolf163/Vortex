#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using Vortex.Sdk.InventorySystem.Controllers;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Чтит лимит, объявленный самим предметом (<see cref="ILimitedProperty"/>): суммарное количество
    /// этого типа в инвентаре не должно превысить его предел. Предмет без свойства лимита не
    /// ограничивается; инвентарь без этого правила лимит игнорирует — ограничение опциональное, как и
    /// остальные верификаторы.
    ///
    /// Про стеки правило не знает: текущий объём берётся агрегатом
    /// <see cref="InventoryController.CountOf"/> (сумма по всем пачкам), вклад входящего — его
    /// количеством в единицах (<see cref="StackController.CountOf"/> отдаёт 1 для нестекуемого).
    /// Оси измерения на весь инвентарь у правила нет — лимит по типу, а не общий, — поэтому
    /// <see cref="GetMax"/>/<see cref="GetCurrent"/> вырождены в 0, как у прочих фильтров.
    /// </summary>
    [Serializable, InfoBox("Чтит лимит на количество, объявленный самим предметом.")]
    public class LimitedVerifier : InventoryVerifier
    {
        public override bool CanPlace(Inventory inventory, ItemModel item)
        {
            var limited = item.GetProperty<ILimitedProperty>();
            if (limited == null)
                return true;

            var current = inventory.CountOf(item.GuidPreset);
            var incoming = item.CountOf();
            return current + incoming <= limited.Limit;
        }

        public override long GetMax(Inventory inventory) => 0;
        public override long GetCurrent(Inventory inventory) => 0;
    }
}
#endif
