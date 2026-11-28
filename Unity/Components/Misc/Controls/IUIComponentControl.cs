using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.Components.Misc.Controls
{
    /// <summary>
    /// Контракт управляющего скрипта: целевой <see cref="UIComponent"/>, на который скрипт выставляет свою
    /// постоянную настройку, и вход повторного выставления этой настройки.
    /// </summary>
    internal interface IUIComponentControl
    {
        /// <summary>Целевой <see cref="UIComponent"/>, на который скрипт выставляет свою постоянную настройку.</summary>
        UIComponent Target { get; }

        /// <summary>Повторно выставить постоянную настройку скрипта на <see cref="Target"/>.</summary>
        void Apply();
    }
}
