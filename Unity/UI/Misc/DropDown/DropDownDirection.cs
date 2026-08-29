namespace Vortex.Unity.UI.Misc.DropDown
{
    /// <summary>
    /// Направление раскрытия списка от кнопки (угол привязки). <see cref="RightDown"/> — обязательное
    /// значение по умолчанию и фолбек при отсутствии точки под другое направление. Порядок значений
    /// совпадает с порядком состояний UIStateSwitcher вида вывода (RightDown = 0 … LeftTop = 3).
    /// </summary>
    public enum DropDownDirection
    {
        RightDown,
        RightTop,
        LeftDown,
        LeftTop
    }
}
