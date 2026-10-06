using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Контракт одной цели фокус-навигации. Реализуется MonoBehaviour-компонентом
    /// (<see cref="FocusTargetComponent"/>) или любым объектом, который хочет быть
    /// фокусируемым виртуальным курсором.
    ///
    /// Реестр целей живёт в <c>VirtualCursorFocusController</c>; регистрация идёт при
    /// <c>OnEnable</c>, снятие — при <c>OnDisable</c>. Контроллер зовёт
    /// <see cref="NotifyFocused"/>/<see cref="NotifyUnfocused"/> на переходах фокуса.
    /// </summary>
    public interface IFocusTarget
    {
        /// <summary>
        /// Экранная точка цели в пикселях (центр для hover+warp курсора). Вычисляется
        /// при каждом запросе — для UGUI через <see cref="RectTransform"/> на overlay-
        /// канвасе, для world-объекта через <see cref="Camera.WorldToScreenPoint"/>.
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

        /// <summary>Контроллер зовёт при уходе фокуса (другой target или <c>ClearFocus</c>).</summary>
        void NotifyUnfocused();
    }
}
