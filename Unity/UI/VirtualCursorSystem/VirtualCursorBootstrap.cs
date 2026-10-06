using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Бутстрап пакета: грузит конфиг скинов и инициализирует контроллер + параметры проекции.
    /// Вешается на объект в сцене/префабе загрузки (рядом с EventSystem/UI-корнем).
    /// </summary>
    [RequireComponent(typeof(VirtualPointerDispatcher))]
    public class VirtualCursorBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Конфиг скинов курсора (SO).")]
        private CursorSkinSettings settings;

        [SerializeField, Tooltip("Маска слоёв для screen→world проекции.")]
        private LayerMask projectionMask = ~0;

        [SerializeField, Tooltip("Дистанция raycast проекции.")]
        private float projectionDistance = 1000f;

        // Android + сценарии ввода (базовый вариант закрыт TouchInputDriver + hide-by-source;
        // осталось доработать первичное состояние и переключение источников):
        //   • Android + ТАЧ: TouchInputDriver в режиме HideOnly (дефолтный). На касание выставляет
        //     source=Point + hide, но ScreenPosition НЕ трогает — VirtualPointerDispatcher не делает
        //     raycast, UGUI обрабатывает клик нативным путём (InputSystemUIInputModule на <Touchscreen>).
        //     Двойных кликов и фантомных позиций нет. Initial-flash (дефолтный спрайт в (0,0) до
        //     первого ввода) закрыт в UiImageCursorRenderer через гейт _firstReportReceived — визуал
        //     стартует скрытым. На пустом тач-девайсе визуал так и остаётся скрытым: HideOnly
        //     репортит только source, OnPosition у рендерера не вызывается — гейт не снимается.
        //   • Desktop + тач-экран (ноутбуки, Surface): TouchInputDriver в режиме Delta — палец
        //     работает как трекпад (относительное смещение), курсор остаётся видимым. Абсолютный
        //     прыжок в точку касания отключён, конфликта с одновременной мышью нет.
        //   • Android + МЫШЬ (BT/USB): MouseInputDriver — через last-source-wins курсор снова виден,
        //     рендер UGUI-Image работает независимо от ОС-указателя (на Android Cursor.SetCursor —
        //     no-op, поэтому собственный Image это единственный способ показать курсор).
        //   • Android + ГЕЙМПАД: DirectInputDriver — двигает виртуальный курсор напрямую через
        //     ReportPointer, не завязан на наличие ОС-мыши. UINavigationDriver — альтернативное
        //     управление через фокус-навигацию (без курсора).
        //
        // TODO(android-cursor): платформенного гейта на бутстрап нет — на чисто-тач устройстве
        // VirtualCursorBootstrap/Dispatcher/Renderer поднимаются всегда. HidesCursor скрывает визуал,
        // но объекты всё равно висят. Это ок (нет overhead), но при желании можно гейтить
        // через `SupportsPlatform` на InputDriver-уровне (уже есть) и/или добавить флаг на Bootstrap.
        private void Awake()
        {
            VirtualCursorController.Init(settings);
            VirtualCursorController.ConfigureProjection(projectionMask, projectionDistance);
            // Фокус-навигация — подсистема того же пакета. Init после основного контроллера:
            // владеет FocusModel, подписывается на ScreenPosition (которая уже существует
            // к этому моменту). Безопасен при повторном вызове (идемпотентен).
            VirtualCursorFocusController.Init();
        }
    }
}
