using UnityEngine;
using Vortex.Unity.UI.VirtualCursorSystem.Handlers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Focus
{
    /// <summary>
    /// Контракт одной цели фокус-навигации. Реализуется MonoBehaviour-компонентом
    /// (<see cref="FocusTargetComponent"/>) или любым объектом, который хочет быть
    /// фокусируемым виртуальным курсором.
    ///
    /// Цели регистрируются в своей <see cref="FocusGroup.Register"/> (<see cref="FocusGroup"/> /
    /// <see cref="FocusGroup"/>), навигация идёт среди целей активной группы.
    /// Контроллер зовёт <see cref="NotifyUnfocused"/>/<see cref="FocusGroup"/> на переходах фокуса.
    /// </summary>
    public interface IFocusTarget
    {
        /// <summary>
        /// Экранная точка цели в пикселях — куда переносится курсор при фокусе. Вычисляется
        /// при каждом запросе, поэтому следует за смещением цели (скролл, лейаут, анимация).
        /// </summary>
        Vector2 ScreenPoint { get; }

        /// <summary>
        /// Готов ли target принимать фокус. Включает проверку <c>GameObject</c>-активности
        /// (неактивные в иерархии target'ы не участвуют в <c>Navigate</c>) и доступности
        /// камеры для world-целей (без зарегистрированной <c>CameraProvider</c>-камеры
        /// screen-point не вычислить → <c>IsActive = false</c>).
        /// </summary>
        bool IsActive { get; }

        /// <summary>Контроллер зовёт при переходе фокуса на этот target.</summary>
        void NotifyFocused();

        /// <summary>Контроллер зовёт при уходе фокуса (на другой target или при снятии фокуса).</summary>
        void NotifyUnfocused();

        /// <summary>
        /// GameObject, в который <see cref="VirtualCursorFocusController.Submit"/> эмулирует клик
        /// (PointerDown/Up/Click с подъёмом по иерархии до обработчиков — как при попадании рейкаста).
        /// </summary>
        GameObject Target { get; }
    }
}