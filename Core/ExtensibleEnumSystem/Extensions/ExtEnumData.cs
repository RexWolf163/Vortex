using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Core.ExtensibleEnumSystem.Extensions
{
    /// <summary>
    /// Реактивное значение оси состояния.
    /// Хранит ссылку на конкретный singleton-инстанс <typeparamref name="T"/>.
    /// Сериализуется через custom-конвертер ExtensibleEnum в SerializeController:
    /// в JSON пишется только строка <c>"{FullName}.{Key}"</c>.
    /// </summary>
    public class ExtEnumData<T> : ReactiveValue<T> where T : ExtensibleEnum
    {
        public ExtEnumData()
        {
        }

        public ExtEnumData(T initial) => Set(initial);

        /// <summary>Ключ текущего значения, либо <c>null</c>.</summary>
        public string Key => Value?.Key;

        /// <summary>Индекс текущего значения в осевом порядке, либо <c>-1</c>.</summary>
        public int Index => Value?.Order ?? -1;

        /// <summary>Сравнение по ссылке (singleton-инстансы).</summary>
        public bool Is(T other) => ReferenceEquals(Value, other);

        /// <summary>Сравнение по ключу.</summary>
        public bool IsKey(string key) => Value != null && Value.Key == key;
    }
}