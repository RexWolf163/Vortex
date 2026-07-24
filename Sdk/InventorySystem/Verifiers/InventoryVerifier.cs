#if USING_VORTEX_ITEMS
using System;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Правило, решающее, пускать ли предмет в инвентарь. Задаётся полиморфно в наборе инвентаря
    /// (<c>[SerializeReference]</c>): дизайнер выбирает класс, как выбирает класс свойства предмета.
    /// Правило, которого нет в наборе, ничего не ограничивает — безлимит выражается отсутствием,
    /// а не условным числом.
    ///
    /// Инвентарь приходит параметром во все методы: ссылку на него правило не хранит — она не нужна
    /// ни одной операции и замкнула бы цикл в сохранении. Впрочем, верификаторы — настройка и в
    /// сохранение не идут вовсе (в модели инвентаря они хранятся полем, а не свойством).
    /// </summary>
    [Serializable, ClassLabel("$Label")]
    public abstract class InventoryVerifier
    {
        /// <summary>Заголовок элемента в списке верификаторов инспектора — имя конкретного правила.</summary>
        protected string Label => GetType().Name;

        /// <summary>Пустить ли предмет. Инвентарь опрашивает набор и останавливается на первом отказе.</summary>
        public abstract bool CanPlace(Inventory inventory, ItemModel item);

        /// <summary>
        /// Предел измеряемой величины для интерфейса. Правила-фильтры, ничего не измеряющие,
        /// отдают 0 — это документированное вырождение, а не сигнальное значение.
        /// </summary>
        public abstract long GetMax(Inventory inventory);

        /// <summary>Текущее значение измеряемой величины на составе. Фильтры отдают 0.</summary>
        public abstract long GetCurrent(Inventory inventory);
    }
}
#endif
