using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Класс фиксации данных по состоянию клавиш и ховера
    /// </summary>
    public class MouseKeyMap
    {
        public BoolData LeftKeyPressed { get; internal set; } = new(false);
        public BoolData RightKeyPressed { get; internal set; } = new(false);
        public IntData HoverIndex { get; internal set; } = new(-1);
    }
}