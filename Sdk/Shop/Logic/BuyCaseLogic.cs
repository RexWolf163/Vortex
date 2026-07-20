using System;
using Cysharp.Threading.Tasks;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Logic
{
    /// <summary>
    /// Логика выдачи товара. Настраивается на пресете через [SerializeReference]. Сколько выдавать
    /// за единицу — параметр конкретной реализации, не пресета. Тот же контракт stateless / отсекающая
    /// предпроверка / единый сохраняемый контур, что и у <see cref="PaymentLogic"/>.
    ///
    /// Для повтора выдачи после перезапуска реализация обязана опираться только на данные события
    /// журнала (requestedCount, buyValue), передаваемые через контекст, — собственного состояния у неё нет.
    /// </summary>
    [Serializable]
    public abstract class BuyCaseLogic
    {
        /// <summary>
        /// Синхронная предпроверка возможности выдачи запрошенного количества.
        /// null — можно; иначе код отказа (обычно DeliveryUnavailable).
        /// </summary>
        public abstract ShopRefusal? CanDeliver(int requestedCount);

        /// <summary>
        /// Выполнить выдачу. Исход докладывается через <paramref name="context"/>:
        /// Delivered / Ready / Pending / Failed. При Failed контроллер применяет политику фолбэка.
        /// </summary>
        public abstract UniTask Deliver(IBuyContext context);
    }
}
