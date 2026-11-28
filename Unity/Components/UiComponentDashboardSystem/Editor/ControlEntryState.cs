#if UNITY_EDITOR
namespace Vortex.Unity.Components.UiComponentDashboardSystem.Editor
{
    /// <summary>
    /// Вариант вывода управляющего скрипта в группе панели дашборда.
    /// </summary>
    internal enum ControlEntryState
    {
        /// <summary>Вид органов управления скрипта.</summary>
        Ok = 0,

        /// <summary>Строка-ошибка: ссылка скрипта на целевой компонент пуста.</summary>
        NoTarget = 1,

        /// <summary>Строка-ошибка: ссылка скрипта указывает на компонент другого GameObject.</summary>
        ForeignTarget = 2,

        /// <summary>Строка «тип — вида нет»: для типа скрипта не зарегистрирован вид.</summary>
        NoView = 3
    }
}
#endif
