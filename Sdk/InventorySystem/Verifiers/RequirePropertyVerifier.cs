#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Verifiers
{
    /// <summary>
    /// Фильтр «требуется свойство такого назначения». Обобщённый: интерфейс назначения — это тип, и
    /// в инспекторе его не выбрать, зато полиморфный выбор работает по классу. Проект объявляет
    /// наследника в одну строку, закрыв параметр конкретным интерфейсом, и получает готовый фильтр
    /// без единого поля:
    /// <code>
    /// [Serializable] public class RequireQuestTag : RequirePropertyVerifier&lt;IQuestTag&gt; { }
    /// </code>
    /// </summary>
    [Serializable, InfoBox("Требуется свойство указанного назначения.")]
    public abstract class RequirePropertyVerifier<T> : InventoryVerifier where T : class, IItemProperty
    {
        public override bool CanPlace(Inventory inventory, ItemModel item) => item.HasProperty<T>();

        public override long GetMax(Inventory inventory) => 0;
        public override long GetCurrent(Inventory inventory) => 0;
    }
}
#endif
