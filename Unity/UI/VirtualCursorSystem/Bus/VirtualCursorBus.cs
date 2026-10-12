using Vortex.Unity.UI.VirtualCursorSystem.Focus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Bus
{
    /// <summary>
    /// Публичный фасад пакета: держит runtime-модели курсора (<see cref="Data"/>), фокус-навигации
    /// (<see cref="Focus"/>) и жестов мультикасания (<see cref="Gesture"/>). Снаружи — только чтение и подписка на реактивные поля моделей.
    /// Запись идёт через контроллеры (<c>VirtualCursorController</c>, <c>VirtualCursorFocusController</c>),
    /// которые закрывают поля моделей на себя как владельцы.
    ///
    /// Модели создаются статически и доступны с первого обращения; контроллеры — подключаемая часть.
    /// </summary>
    public static class VirtualCursorBus
    {
        /// <summary>
        /// Имя скина скрытого курсора. Контроллер выставляет его в <c>Skin</c>, когда активный источник
        /// ввода скрывает курсор; рендер должен иметь скин с этим именем.
        /// </summary>
        public const string HideCursor = "hidden";

        /// <summary>
        /// Runtime-модель указателя: позиция, действия, скин, скролл. Владельцем полей становится
        /// <c>VirtualCursorController</c> при инициализации (контроллер единственный).
        /// </summary>
        public static PointerModel Data { get; private set; } = new();

        /// <summary>
        /// Runtime-модель фокус-навигации: стек групп и текущий фокус. Владельцем становится
        /// <c>VirtualCursorFocusController</c> при инициализации (контроллер единственный).
        /// </summary>
        public static FocusModel Focus { get; private set; } = new();

        /// <summary>
        /// Runtime-модель жестов мультикасания: последний кадр жеста. Владельцем становится
        /// <c>VirtualCursorController</c> при инициализации.
        /// </summary>
        public static GestureModel Gesture { get; private set; } = new();
    }
}