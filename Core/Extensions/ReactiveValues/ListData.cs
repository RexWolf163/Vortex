using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Канонический реактивный список — наследник <see cref="ReactiveCollection{T}"/>
    /// с инициализирующими конструкторами. Сама база <see cref="ReactiveCollection{T}"/>
    /// конструкторов не предоставляет (хранилище <c>Value</c> остаётся <c>null</c>),
    /// поэтому в коде модели обычно объявляется именно <see cref="ListData{T}"/>.
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
        /// <summary>Пустой список.</summary>
        public ListData() => Value = new List<T>();

        /// <summary>
        /// Список с начальным содержимым. Хранится <b>копия</b> переданного <paramref name="value"/>
        /// (через <see cref="Enumerable.ToList{TSource}"/>) — мутации источника не влияют на контейнер.
        /// </summary>
        public ListData(List<T> value)
        {
            Value = value.ToList();
        }
    }
}
