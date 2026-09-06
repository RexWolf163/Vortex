using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Виртуальный UI-указатель — подкласс <see cref="Mouse"/>: полная поддержка родного UGUI
    /// (все кнопки/скролл/позиция; <c>InputSystemUIInputModule</c> работает как с мышью). Отдельный LAYOUT
    /// (<c>&lt;VirtualUiPointer&gt;</c>) нужен, чтобы UI-модуль биндился ИМЕННО на него, а `MousePointerDriver`
    /// (`&lt;Mouse&gt;`) отфильтровывал его по типу устройства (иначе feedback-петля). Состояние пишет
    /// <see cref="UiPointerFeeder"/> из ScreenPosition/ActionState.
    ///
    /// Проектная проводка: в UI-Actions забиндить Point → <c>&lt;VirtualUiPointer&gt;/position</c>,
    /// Left Click → <c>.../leftButton</c>, Right → <c>rightButton</c>, Middle/Forward/Back, ScrollWheel → <c>scroll</c>.
    /// </summary>
    public class VirtualUiPointer : Mouse
    {
        private static bool _registered;

        /// <summary>Регистрация layout один раз (идемпотентно). Зовёт <see cref="UiPointerFeeder"/> перед AddDevice.</summary>
        internal static void EnsureRegistered()
        {
            if (_registered)
                return;
            InputSystem.RegisterLayout<VirtualUiPointer>();
            _registered = true;
        }
    }
}
