using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// UGUI-биндинг действий курсора на <see cref="UnityEvent"/>: вешается на любой UGUI-элемент,
    /// в инспекторе выбирается слушаемый <see cref="PointerAction"/> и UnityEvent'ы для
    /// press/release/click. Выделяет из стандартных UGUI-событий именно нужный тип кнопки:
    /// Button-наследник ловит только Left и годен для <see cref="PointerAction.BaseClick"/>; этот
    /// же компонент одинаково покрывает <see cref="PointerAction.ContextClick"/>/<see cref="PointerAction.AltClick"/>
    /// (RMB/MMB), для которых стандартного Button нет.
    ///
    /// Реагирует на стандартные UGUI-события указателя от любого источника (например,
    /// <c>InputSystemUIInputModule</c>); кнопка определяется по <see cref="PointerEventData.button"/>.
    ///
    /// <b>Ограничения.</b> <see cref="PointerEventData.InputButton"/> поддерживает только
    /// Left/Right/Middle — им соответствуют <see cref="PointerAction.BaseClick"/>,
    /// <see cref="PointerAction.ContextClick"/> и <see cref="PointerAction.AltClick"/>.
    ///
    /// <b>Чужая кнопка уходит вверх.</b> UGUI отдаёт Down/Up/Click первому обработчику вверх по иерархии —
    /// этому компоненту, даже если кнопка не его. Событие чужой кнопки он передаёт дальше по иерархии от родителя:
    /// компонент с <see cref="PointerAction.ContextClick"/> на потомке <c>Button</c> не глушит её левый клик.
    /// Своя кнопка поглощается, как у любого UGUI-обработчика.
    /// </summary>
    [AddComponentMenu("Vortex/Virtual Cursor/Pointer Action Handler")]
    public class PointerActionHandler : MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Tooltip("Действие курсора, которое ловит handler. Поддерживаются: " +
                                 "Action1 (LMB), Action2 (RMB), Action3 (MMB). Остальные через UGUI " +
                                 "не проходят — handler тихо игнорирует.")]
        private PointerAction action = PointerAction.BaseClick;

        [Tooltip("Передний фронт нажатия — приходит синхронно с PointerDown.")]
        public UnityEvent onPressed;

        [Tooltip("Задний фронт отпускания — приходит синхронно с PointerUp (независимо от того, " +
                 "где произошёл Up — на этом target'е или нет; это отличает от onClick).")]
        public UnityEvent onReleased;

        [Tooltip("Полноценный клик — Up произошёл на том же target'е, что и Down (стандартный " +
                 "UGUI-канон, аналог Button.onClick).")]
        public UnityEvent onClick;

        /// <summary>PointerDown нужной кнопки → <see cref="onPressed"/>; чужой — дальше по иерархии.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (Match(eventData)) onPressed?.Invoke();
            else PassUp(eventData, ExecuteEvents.pointerDownHandler);
        }

        /// <summary>PointerUp нужной кнопки → <see cref="onReleased"/>; чужой — дальше по иерархии.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (Match(eventData)) onReleased?.Invoke();
            else PassUp(eventData, ExecuteEvents.pointerUpHandler);
        }

        /// <summary>PointerClick нужной кнопки → <see cref="onClick"/>; чужой — дальше по иерархии.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Match(eventData)) onClick?.Invoke();
            else PassUp(eventData, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>Передать событие ближайшему обработчику выше по иерархии (от родителя).</summary>
        private void PassUp<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
        {
            var parent = transform.parent;
            if (parent != null)
                ExecuteEvents.ExecuteHierarchy(parent.gameObject, eventData, handler);
        }

        /// <summary>
        /// Маппинг Vortex-<see cref="PointerAction"/> на UGUI-<see cref="PointerEventData.InputButton"/>:
        /// BaseClick→Left, ContextClick→Right, AltClick→Middle. <see cref="PointerAction.None"/> — false (игнор).
        /// </summary>
        private bool Match(PointerEventData eventData) => action switch
        {
            PointerAction.BaseClick => eventData.button == PointerEventData.InputButton.Left,
            PointerAction.ContextClick => eventData.button == PointerEventData.InputButton.Right,
            PointerAction.AltClick => eventData.button == PointerEventData.InputButton.Middle,
            _ => false,
        };
    }
}
