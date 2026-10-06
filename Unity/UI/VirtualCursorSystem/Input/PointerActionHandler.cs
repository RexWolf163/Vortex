#if USING_VORTEX_CURSOR
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// UGUI-биндинг действий курсора на <see cref="UnityEvent"/>: вешается на любой UGUI-элемент,
    /// в инспекторе выбирается слушаемый <see cref="PointerAction"/> и UnityEvent'ы для
    /// press/release/click. Выделяет из стандартных UGUI-событий именно нужный тип кнопки:
    /// Button-наследник ловит только Left и годен для <see cref="PointerAction.Action1"/>; этот
    /// же компонент одинаково покрывает <see cref="PointerAction.Action2"/>/<see cref="PointerAction.Action3"/>
    /// (RMB/MMB), для которых стандартного Button нет.
    ///
    /// Работает одновременно с физической мышью (через <c>InputSystemUIInputModule</c>) и
    /// с виртуальным курсором (через <see cref="VirtualPointerDispatcher"/>) — оба источника
    /// шлют стандартные UGUI-события с корректным <see cref="PointerEventData.button"/>.
    ///
    /// <b>Ограничения.</b> <see cref="PointerEventData.InputButton"/> поддерживает только
    /// Left/Right/Middle, поэтому через UGUI-пайплайн доступны только Action1/Action2/Action3.
    /// Для Back/Forward/Scroll/Action8-10 нужен другой паттерн — прямой биндинг на
    /// <c>VirtualCursorBus.Data.Actions.OnUpdate</c> с ручной проверкой hover-зоны.
    /// </summary>
    [AddComponentMenu("Vortex/Virtual Cursor/Pointer Action Handler")]
    public class PointerActionHandler : MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Tooltip("Действие курсора, которое ловит handler. Поддерживаются: " +
                                 "Action1 (LMB), Action2 (RMB), Action3 (MMB). Остальные через UGUI " +
                                 "не проходят — handler тихо игнорирует.")]
        private PointerAction action = PointerAction.Action1;

        [Tooltip("Передний фронт нажатия — приходит синхронно с PointerDown.")]
        public UnityEvent onPressed;

        [Tooltip("Задний фронт отпускания — приходит синхронно с PointerUp (независимо от того, " +
                 "где произошёл Up — на этом target'е или нет; это отличает от onClick).")]
        public UnityEvent onReleased;

        [Tooltip("Полноценный клик — Up произошёл на том же target'е, что и Down (стандартный " +
                 "UGUI-канон, аналог Button.onClick).")]
        public UnityEvent onClick;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Match(eventData)) onPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Match(eventData)) onReleased?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Match(eventData)) onClick?.Invoke();
        }

        /// <summary>
        /// Маппинг Vortex-<see cref="PointerAction"/> на UGUI-<see cref="PointerEventData.InputButton"/>:
        /// Action1→Left, Action2→Right, Action3→Middle. Остальные значения — false (игнор).
        /// </summary>
        private bool Match(PointerEventData eventData) => action switch
        {
            PointerAction.Action1 => eventData.button == PointerEventData.InputButton.Left,
            PointerAction.Action2 => eventData.button == PointerEventData.InputButton.Right,
            PointerAction.Action3 => eventData.button == PointerEventData.InputButton.Middle,
            _ => false,
        };
    }
}
#endif
