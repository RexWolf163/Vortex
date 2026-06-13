using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Наблюдаемая модель состояния мыши: нажатие LMB/RMB и активный hover-индекс.
    /// Создаётся один раз внутри <see cref="CursorController.MouseKeys"/>, доступна
    /// только для чтения снаружи — запись закрыта через <c>SetOwner</c> (см.
    /// <see cref="Vortex.Core.Extensions.ReactiveValues.ReactiveValue{T}.SetOwner"/>).
    ///
    /// Подписка снаружи: <c>MouseKeys.LeftKeyPressed.OnUpdate += handler</c>.
    /// </summary>
    public class MouseKeyMap
    {
        /// <summary>Левая кнопка мыши нажата прямо сейчас.</summary>
        public BoolData LeftKeyPressed { get; internal set; } = new(false);

        /// <summary>Правая кнопка мыши нажата прямо сейчас.</summary>
        public BoolData RightKeyPressed { get; internal set; } = new(false);

        /// <summary>
        /// Индекс активной hover-зоны (соответствует индексу в <see cref="CursorPack.CursorOnHover"/>).
        /// <c>-1</c> означает «hover отсутствует».
        /// </summary>
        public IntData HoverIndex { get; internal set; } = new(-1);
    }
}