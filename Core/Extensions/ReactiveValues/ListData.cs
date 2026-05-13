using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Канонический реактивный список — наследник абстрактного <see cref="ReactiveCollection{T}"/>
    /// с инициализирующими конструкторами. База инстанцироваться не может; для использования
    /// в моделях объявляется именно <see cref="ListData{T}"/>.
    /// </summary>
    /// <typeparam name="T">Тип элемента списка.</typeparam>
    /// <example>
    /// <code>
    /// public class InventoryModel
    /// {
    ///     public ListData&lt;ItemId&gt; Items { get; private set; } = new();
    /// }
    ///
    /// inventory.Items.Add(itemId);
    /// inventory.Items.OnUpdate += list =&gt; RefreshUI(list);
    /// </code>
    /// </example>
    public class ListData<T> : ReactiveCollection<T>
    {
        /// <summary>Создаёт пустой список.</summary>
        public ListData() => Value = new List<T>();

        /// <summary>
        /// Создаёт список с начальным содержимым. Хранится <b>копия</b> переданного
        /// <paramref name="value"/> (через <see cref="Enumerable.ToList{TSource}"/>) —
        /// последующие мутации внешнего источника не влияют на контейнер.
        /// <paramref name="value"/> = <c>null</c> бросит <see cref="System.NullReferenceException"/>;
        /// для пустого списка используйте параметрless-конструктор.
        /// </summary>
        public ListData(List<T> value)
        {
            Value = value.ToList();
        }
    }
}
