#if USING_VORTEX_ITEMS
using System;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Sdk.InventorySystem.Properties;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Controllers
{
    /// <summary>
    /// Контроллер пачек — единственный, кто меняет количество. Держит owner-ключ, которым закрыто
    /// реактивное количество: изменение в обход контроллера не сдвинуло бы отметку свойства, и кеш
    /// занятого начал бы врать в пользу игрока.
    /// </summary>
    public static class StackController
    {
        private static readonly object Key = new();

        private static StackProperty Stack(ItemModel item) => item.GetProperty<IStackProperty>() as StackProperty;

        /// <summary>Количество в пачке предмета. Нет свойства пачки — единица.</summary>
        internal static int CountOf(ItemModel item) => Stack(item)?.Count ?? 1;

        /// <summary>Стекуем ли предмет — есть ли у него свойство пачки.</summary>
        internal static bool IsStackable(ItemModel item) => Stack(item) != null;

        /// <summary>Свободное место в пачке предмета. Нет пачки — ноль.</summary>
        internal static int RoomIn(ItemModel item)
        {
            var stack = Stack(item);
            return stack == null ? 0 : stack.Max - stack.Count;
        }

        /// <summary>
        /// Установить количество, закрыв пачку на контроллер при первом обращении. Отметка свойства
        /// сдвигается только при реальном изменении: количество делает недействительными производные
        /// величины (масса, объём), а запись того же значения ничего не меняет и ось двигать не должна.
        /// </summary>
        internal static void SetCount(ItemModel item, int count)
        {
            var stack = Stack(item);
            if (stack == null)
                return;

            var before = stack.Count;
            stack.SetCount(count, Key);
            if (stack.Count != before)
                stack.Touch();
        }

        /// <summary>
        /// Положить количество из стартового наполнения. Больше <see cref="IStackProperty.Max"/> не
        /// кладётся: количество обрезается до предела с ошибкой в лог — на руках все данные настройки,
        /// и громкая обрезка безопаснее пере-стека, протекающего в рантайм. Многопачечный стартовый
        /// набор задаётся несколькими записями. Нестекуемому предмету количество больше единицы
        /// бессмысленно — он остаётся одним экземпляром, тоже с ошибкой в лог.
        /// </summary>
        internal static void ApplyStartupCount(ItemModel item, int requested)
        {
            var stack = Stack(item);
            if (stack == null)
            {
                if (requested > 1)
                    Log.Print(LogLevel.Error,
                        $"[Inventory] Startup count {requested} for non-stackable item {item.GuidPreset}; placed one.",
                        null);
                return;
            }

            var clamped = Math.Min(requested, stack.Max);
            if (requested > stack.Max)
                Log.Print(LogLevel.Error,
                    $"[Inventory] Startup count {requested} exceeds stack max {stack.Max} for {item.GuidPreset}; clamped.",
                    null);

            SetCount(item, clamped);
        }
    }
}
#endif
