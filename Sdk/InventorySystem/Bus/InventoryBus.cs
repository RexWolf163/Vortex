#if USING_VORTEX_ITEMS
using System;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Sdk.InventorySystem.Model;

namespace Vortex.Sdk.InventorySystem.Bus
{
    /// <summary>
    /// Шина инвентарей — статический контроллер без слота драйвера (подменять нечего, логика
    /// платформонезависимая). Держит событие уничтожения инвентаря: подписчики убирают за
    /// уничтоженным контейнером — закрывают его окно, снимают ссылки. Событие приходит уже после
    /// удаления содержимого, поэтому подбирать из него нечего.
    /// </summary>
    public static class InventoryBus
    {
        /// <summary>Инвентарь уничтожен вместе с содержимым. Для уборки, не для подбора.</summary>
        public static event Action<Inventory> OnInventoryDestroyed;

        internal static void NotifyDestroyed(Inventory inventory) => OnInventoryDestroyed.Fire(inventory);
    }
}
#endif
