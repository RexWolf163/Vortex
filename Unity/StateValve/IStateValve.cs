using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.StateValve
{
    /// <summary>
    /// Точка ссылки на клапан состояния: производители зовут <see cref="Open"/>/<see cref="Close"/>,
    /// потребители подписываются на <see cref="State"/>. Реализуется <c>StateValveHandler</c>, который
    /// делегирует в Core-примитив.
    /// </summary>
    public interface IStateValve
    {
        /// <summary>Открыть створку по имени.</summary>
        void Open(string id);

        /// <summary>Закрыть створку по имени.</summary>
        void Close(string id);

        /// <summary>Реактивный итог: открыт/закрыт. Только чтение и подписка.</summary>
        BoolData State { get; }

        /// <summary>Белый список разрешённых ключей — источник дропдауна у запирающих вьюшек. Пусто = фильтра нет.</summary>
        string[] GetWhiteList();
    }
}
