using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Наблюдаемая модель состояния мыши: нажатие LMB/RMB и активный hover-ключ.
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
        /// Ключ активной hover-зоны — совпадает с <see cref="CursorHoverEntry.Name"/> из
        /// <see cref="CursorPack.CursorOnHover"/>. Пусто/null означает «hover отсутствует».
        /// </summary>
        public StringData HoverKey { get; internal set; } = new(null);
    }
}