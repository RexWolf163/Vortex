#if USING_VORTEX_ITEMS
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Model;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.InventorySystem.Controllers
{
    /// <summary>
    /// Отладочный реестр живых инвентарей. Существует только ради поиска дубликатов — одного
    /// экземпляра предмета, попавшего в два инвентаря сразу. Защиту от такого нельзя поставить в
    /// предмет: это развернуло бы зависимость сборок. Поэтому проверка вынесена в отладочный контур
    /// и носит диагностический характер.
    ///
    /// Регистрируются только материализованные инвентари — только они держат живые ссылки на
    /// предметы и только между ними возможен дубль по ссылке. Регистрация ведётся лишь при
    /// включённом флаге, чтобы в релизе не удерживать ссылки.
    /// </summary>
    public static class InventoryRegistry
    {
        private static readonly HashSet<Inventory> Live = new();

        private static bool Enabled => Settings.Data().InventoryDebugMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bootstrap()
        {
            GameController.OnNewGame -= Reset;
            GameController.OnNewGame += Reset;
            GameController.OnLoadGame -= ScanDuplicates;
            GameController.OnLoadGame += ScanDuplicates;
        }

        internal static void Register(Inventory inventory)
        {
            if (Enabled)
                Live.Add(inventory);
        }

        internal static void Unregister(Inventory inventory) => Live.Remove(inventory);

        private static void Reset()
        {
            Live.Clear();
            ScanDuplicates();
        }

        private static void ScanDuplicates()
        {
            if (!Enabled)
                return;

            // Снимок: обход вложенных контейнеров материализует их и дописывает в Live,
            // а менять коллекцию во время перебора нельзя. Вложенные и так достаются рекурсией.
            var snapshot = new List<Inventory>(Live);
            var seen = new HashSet<ItemModel>();
            foreach (var inventory in snapshot)
                ScanInventory(inventory, seen);
        }

        private static void ScanInventory(Inventory inventory, HashSet<ItemModel> seen)
        {
            foreach (var item in inventory.Items)
            {
                if (!seen.Add(item))
                    Log.Print(LogLevel.Error,
                        $"[Inventory] Duplicate item instance across inventories: preset {item.GuidPreset}", null);

                var container = item.GetProperty<IContainerProperty>();
                if (container != null)
                    ScanInventory(container.Inventory, seen);
            }
        }
    }
}
#endif
