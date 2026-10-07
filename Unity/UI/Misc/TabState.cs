namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Состояние вкладки в <see cref="TabsHandler"/>. Порядок членов задаёт номера состояний
    /// свитчера вкладки — слоты в <c>UIStateSwitcher</c> должны идти в этом же порядке.
    /// </summary>
    public enum TabState
    {
        /// <summary>Вкладка выключена: выбрать нельзя, перебор её пропускает.</summary>
        Disabled = 0,

        /// <summary>Вкладка доступна, но не выбрана.</summary>
        Enabled = 1,

        /// <summary>Выбранная вкладка. Такая всегда одна.</summary>
        Selected = 2
    }
}
