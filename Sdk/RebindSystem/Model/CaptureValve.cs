using System.Collections.Generic;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Клапан перехвата — состояние «ждём нажатия для слота» (ТЗ 1.3.8, 2.4). Снаружи только для чтения; меняет
    /// контроллер системы. Каждое изменение — событие шины <c>OnCaptureChanged</c>.
    /// </summary>
    public sealed class CaptureValve
    {
        /// <summary>Клапан ждёт нажатия.</summary>
        public bool IsOpen => BindKey != null;

        /// <summary>Слот, для которого ждём нажатия. <c>null</c> — клапан закрыт.</summary>
        public string BindKey { get; internal set; }

        /// <summary>Удерживаемые модификаторы в порядке нажатия — пути со стороной по настройке.</summary>
        public IReadOnlyList<string> HeldModifiers => Held;

        /// <summary>
        /// Последний ответ: отказ при открытом клапане (запрещённая клавиша, недопустимый триггер, лишние
        /// модификаторы) или итог закрытия — <c>Applied</c>, <c>Rejected</c> (внутрикартовый конфликт),
        /// <c>Cancelled</c>. <c>null</c> — после открытия ответа ещё не было.
        /// </summary>
        public RebindResult LastResult { get; internal set; }

        internal List<string> Held { get; } = new();
    }
}
