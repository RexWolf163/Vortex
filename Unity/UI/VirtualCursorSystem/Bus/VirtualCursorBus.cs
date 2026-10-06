using System;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Публичный фасад пакета: доступ к runtime-модели, текущему виду курсора и готовности.
    /// Только чтение + события. Интейк (репорт позиции/действий) — на контроллере (internal, для драйверов).
    /// </summary>
    public static class VirtualCursorBus
    {
        public static PointerModel Data => VirtualCursorController.Model;
        public static CursorVisualData Visual => VirtualCursorController.Visual;
        public static bool IsReady => VirtualCursorController.IsReady;

        /// <summary>
        /// Runtime-модель подсистемы фокус-навигации (<see cref="VirtualCursorFocusController"/>).
        /// Внешние подписчики: <c>Focus.CurrentFocus.OnUpdate</c> — реакция на смену фокуса.
        /// <c>null</c> до инициализации фокус-контроллера (вызывается из <c>VirtualCursorBootstrap</c>).
        /// </summary>
        public static FocusModel Focus => VirtualCursorFocusController.Model;

        public static event Action OnReady
        {
            add => VirtualCursorController.OnReady += value;
            remove => VirtualCursorController.OnReady -= value;
        }
    }
}
