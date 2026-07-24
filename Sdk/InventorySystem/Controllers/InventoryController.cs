#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Bus;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Controllers
{
    /// <summary>
    /// Операции над составом инвентаря — единственный путь его изменения. Перемещение между
    /// инвентарями принадлежит паре и потому живёт здесь, а не на модели. Проверка и размещение
    /// пользуются одним и тем же набором правил, поэтому разойтись не могут.
    /// </summary>
    public static class InventoryController
    {
        #region Проверка

        /// <summary>
        /// Пустит ли инвентарь предмет. Сперва структурная проверка — контейнер нельзя положить в
        /// себя или в свой потомок (иначе расчёт массы и уничтожение ушли бы в бесконечную рекурсию),
        /// затем опрос набора правил с остановкой на первом отказе.
        /// </summary>
        public static bool CanPlace(this Inventory inventory, ItemModel item)
        {
            if (CreatesCycle(inventory, item))
            {
                Log.Print(LogLevel.Error,
                    $"[Inventory] Rejected placing container into itself or its descendant: preset {item.GuidPreset}",
                    null);
                return false;
            }

            foreach (var verifier in inventory.Verifiers)
                if (!verifier.CanPlace(inventory, item))
                    return false;

            return true;
        }

        /// <summary>
        /// Создало бы ли размещение предмета петлю вложенности: является ли инвентарь-приёмник самим
        /// инвентарём предмета-контейнера или его потомком. Некотейнер петли создать не может.
        /// </summary>
        private static bool CreatesCycle(Inventory target, ItemModel item)
        {
            var container = item.GetProperty<IContainerProperty>();
            return container != null && Contains(container.Inventory, target);
        }

        private static bool Contains(Inventory node, Inventory target)
        {
            if (ReferenceEquals(node, target))
                return true;

            foreach (var item in node.Items)
            {
                var container = item.GetProperty<IContainerProperty>();
                if (container != null && Contains(container.Inventory, target))
                    return true;
            }

            return false;
        }

        #endregion

        #region Добавление

        /// <summary>
        /// Добавить предмет. Отказ по вместимости не оставляет следов. Успех складывает в пачки по
        /// политике инвентаря и сдвигает отметку состава.
        /// </summary>
        public static bool Add(this Inventory inventory, ItemModel item)
        {
            if (!inventory.CanPlace(item))
                return false;

            var composition = inventory.Composition;

            if (inventory.Policy == StackPolicy.None || !StackController.IsStackable(item))
                composition.Add(item);
            else
                MergeIn(inventory, composition, item);

            inventory.StampComposition();
            return true;
        }

        private static void MergeIn(Inventory inventory, List<ItemModel> composition, ItemModel incoming)
        {
            var debug = Settings.Data().InventoryDebugMode;
            var remaining = StackController.CountOf(incoming);

            if (inventory.Policy == StackPolicy.Whole)
            {
                foreach (var existing in composition)
                {
                    if (!SameStack(existing, incoming) || StackController.RoomIn(existing) < remaining)
                        continue;
                    if (debug)
                        WarnDivergence(existing, incoming);
                    StackController.SetCount(existing, StackController.CountOf(existing) + remaining);
                    Absorb(incoming);
                    return;
                }

                composition.Add(incoming);
                return;
            }

            // StackPolicy.Merge — доливаем в каждую пачку со свободным местом.
            foreach (var existing in composition)
            {
                if (remaining == 0)
                    break;
                if (!SameStack(existing, incoming))
                    continue;

                var room = StackController.RoomIn(existing);
                if (room <= 0)
                    continue;

                var move = Math.Min(room, remaining);
                if (debug)
                    WarnDivergence(existing, incoming);
                StackController.SetCount(existing, StackController.CountOf(existing) + move);
                remaining -= move;
            }

            if (remaining == 0)
            {
                Absorb(incoming);
                return;
            }

            StackController.SetCount(incoming, remaining);
            composition.Add(incoming);
        }

        private static bool SameStack(ItemModel existing, ItemModel incoming) =>
            existing.GuidPreset == incoming.GuidPreset && StackController.IsStackable(existing);

        /// <summary>
        /// Входящий предмет поглощён существующими пачками целиком и ни одному инвентарю не
        /// принадлежит. Обнуляем его количество: <see cref="Add"/> и <see cref="Move"/> забирают
        /// владение, и если вызывающий по ошибке переиспользует ссылку, поглощённый экземпляр не
        /// внесёт лишних единиц. Экземпляр после этого — пустой призрак, держать его не нужно.
        /// </summary>
        private static void Absorb(ItemModel incoming) => StackController.SetCount(incoming, 0);

        #endregion

        #region Изъятие и уничтожение

        /// <summary>Изъять предмет целиком и вернуть вызывающему. <c>null</c>, если его нет в составе.</summary>
        public static ItemModel Take(this Inventory inventory, ItemModel item)
        {
            if (!inventory.Composition.Remove(item))
                return null;

            inventory.StampComposition();
            return item;
        }

        /// <summary>
        /// Изъять часть пачки. Возвращает новый экземпляр с отделённым количеством; исходная пачка
        /// уменьшается. Количество не меньше пачки означает изъятие целиком. Отделённый экземпляр
        /// строится из пресета — состояние сверх количества (прочность и прочее) на нём не
        /// восстанавливается, как и при слиянии.
        /// </summary>
        public static ItemModel TakeAmount(this Inventory inventory, ItemModel item, int amount)
        {
            if (amount <= 0 || !inventory.Composition.Contains(item))
                return null;

            var count = StackController.CountOf(item);
            if (amount >= count)
                return inventory.Take(item);

            StackController.SetCount(item, count - amount);

            var split = ItemsBus.Create(item.GuidPreset);
            StackController.SetCount(split, amount);

            inventory.StampComposition();
            return split;
        }

        /// <summary>Убрать предмет из состава и уничтожить. Вложенный контейнер уничтожается рекурсивно.</summary>
        public static bool Drop(this Inventory inventory, ItemModel item)
        {
            if (!inventory.Composition.Remove(item))
                return false;

            DestroyItem(item);
            inventory.StampComposition();
            return true;
        }

        #endregion

        #region Агрегатные операции по типу предмета

        /// <summary>
        /// Сколько единиц предмета данного типа в инвентаре суммарно — с учётом пачек: стек из 100
        /// монет считается за 100. Нужно для оплаты предметами-валютой и подсчёта ресурсов.
        /// </summary>
        public static int CountOf(this Inventory inventory, string presetGuid)
        {
            var total = 0;
            foreach (var item in inventory.Items)
                if (item.GuidPreset == presetGuid)
                    total += StackController.CountOf(item);
            return total;
        }

        /// <summary>
        /// Списать заданное число единиц предмета данного типа — сквозь пачки: сначала проверка, что
        /// суммарно хватает, потом расход по стекам (полностью опустошённые изымаются). Ничего не
        /// списывает при нехватке — либо всё, либо ничего.
        /// </summary>
        public static bool RemoveCount(this Inventory inventory, string presetGuid, int amount)
        {
            if (amount <= 0)
                return true;
            if (inventory.CountOf(presetGuid) < amount)
                return false;

            var matches = new List<ItemModel>();
            foreach (var item in inventory.Composition)
                if (item.GuidPreset == presetGuid)
                    matches.Add(item);

            var remaining = amount;
            foreach (var item in matches)
            {
                if (remaining == 0)
                    break;

                var have = StackController.CountOf(item);
                if (have <= remaining)
                {
                    inventory.Take(item);
                    remaining -= have;
                }
                else
                {
                    StackController.SetCount(item, have - remaining);
                    remaining = 0;
                }
            }

            return true;
        }

        /// <summary>
        /// Влезет ли заданное число единиц предмета данного типа — без реального создания предмета и
        /// без побочных эффектов. Строит тихую пробу (<see cref="ItemsBus.BuildProbe"/>: без события,
        /// без сдвига оси), задаёт ей полное количество и прогоняет через набор верификаторов
        /// (<see cref="CanPlace"/>). Для стекуемых предметов точно при любом количестве — масса и
        /// объём линейны по количеству. Для нестекуемых с количеством больше одного проба
        /// представляет один экземпляр, поэтому проверка приблизительна (полную гарантию там даёт
        /// откат в <see cref="AddCount"/>).
        /// </summary>
        public static bool CanAdd(this Inventory inventory, string presetGuid, int amount)
        {
            if (amount <= 0)
                return true;

            var probe = ItemsBus.BuildProbe(presetGuid);
            StackController.SetCountSilent(probe, amount);
            return inventory.CanPlace(probe);
        }

        /// <summary>
        /// Добавить заданное число единиц предмета данного типа, нарезая на пачки не крупнее
        /// <see cref="Properties.IStackProperty.Max"/> (нестекуемый — по одному экземпляру). Каждая
        /// пачка проходит через <see cref="Add"/>, то есть уважает верификаторы и политику склейки.
        ///
        /// Всё-или-ничего: если вместимость исчерпалась на середине (многочанковый случай, когда
        /// <paramref name="amount"/> больше <see cref="Properties.IStackProperty.Max"/>), уже
        /// добавленное снимается обратно и возвращается <c>false</c>. Иначе частичная выдача при
        /// откате оплаты магазином давала бы дублирование. Откат корректен для взаимозаменяемых
        /// (стекуемых) единиц; для нестекуемых предметов с индивидуальным состоянием, покупаемых
        /// пачкой больше одного, снятие может задеть не тот экземпляр — редкий край, вне 1.0.
        /// </summary>
        public static bool AddCount(this Inventory inventory, string presetGuid, int amount)
        {
            if (amount <= 0)
                return true;

            var added = 0;
            var remaining = amount;
            while (remaining > 0)
            {
                var item = ItemsBus.Create(presetGuid);
                var chunk = Math.Min(remaining, Math.Max(1, StackController.MaxOf(item)));
                StackController.SetCount(item, chunk);

                if (!inventory.Add(item))
                {
                    if (added > 0)
                        inventory.RemoveCount(presetGuid, added);
                    return false;
                }

                added += chunk;
                remaining -= chunk;
            }

            return true;
        }

        #endregion

        #region Перемещение

        /// <summary>
        /// Переместить предмет из одного инвентаря в другой. Порядок обязателен: сперва спрашивается
        /// приёмник, и только при согласии предмет изымается из источника. Обратный порядок терял бы
        /// предмет при отказе приёмника.
        /// </summary>
        public static bool Move(this ItemModel item, Inventory from, Inventory to)
        {
            if (!to.CanPlace(item))
                return false;

            if (from.Take(item) == null)
                return false;

            if (to.Add(item))
                return true;

            // Приёмник согласился на проверке, но отказал на размещении — вернуть в источник.
            from.Add(item);
            return false;
        }

        #endregion

        #region Уничтожение состава

        /// <summary>
        /// Удалить всё содержимое, рекурсивно спускаясь во вложенные контейнеры. Вызывается при
        /// уничтожении инвентаря. Под отладочным флагом пишет, сколько и чего удалено.
        /// </summary>
        internal static void PurgeRecursive(Inventory inventory)
        {
            var composition = inventory.Composition;

            if (composition.Count > 0 && Settings.Data().InventoryDebugMode)
                LogPurge(composition);

            foreach (var item in composition)
                DestroyItem(item);

            composition.Clear();
            inventory.StampComposition();
        }

        private static void DestroyItem(ItemModel item)
        {
            var container = item.GetProperty<IContainerProperty>();
            container?.Inventory.Dispose();
        }

        #endregion

        #region Отладка

        private static void WarnDivergence(ItemModel target, ItemModel incoming)
        {
            if (Signature(target) != Signature(incoming))
                Log.Print(LogLevel.Warning,
                    $"[Inventory] Stacking items with divergent state: preset {incoming.GuidPreset}", null);
        }

        private static string Signature(ItemModel item)
        {
            var parts = new List<string>();
            foreach (var property in item.AllProperties)
            {
                if (property is IStackProperty)
                    continue;
                parts.Add($"{property.GetType().FullName}:{property.SerializeProperties()}");
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static void LogPurge(List<ItemModel> composition)
        {
            var counts = new Dictionary<string, int>();
            foreach (var item in composition)
            {
                var count = StackController.CountOf(item);
                counts.TryGetValue(item.GuidPreset, out var acc);
                counts[item.GuidPreset] = acc + count;
            }

            foreach (var (preset, total) in counts)
                Log.Print(LogLevel.Common,
                    $"[Inventory] Destroying inventory content: {total} × {preset}", null);
        }

        #endregion
    }
}
#endif
