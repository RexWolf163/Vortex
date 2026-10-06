#if USING_VORTEX_CURSOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Диспетчер UGUI-событий для виртуального курсора. Читает <c>ScreenPosition</c>
    /// и маску <c>Actions</c> из <see cref="VirtualCursorBus"/>, делает Raycast через
    /// <see cref="EventSystem"/> и шлёт события (<see cref="ExecuteEvents"/>) напрямую
    /// в найденные UI-targets. Без фантомного InputDevice и без отдельного UI Actions-asset
    /// под виртуальный pointer — вся логика на стороне диспетчера.
    ///
    /// Параллельно может работать штатный <c>InputSystemUIInputModule</c>, слушающий
    /// физическую мышь. Два источника UGUI-событий независимы: пользователь двигает
    /// мышью → работает модуль, двигает стиком → работает этот диспетчер. Target
    /// получает Enter/Exit/Click от обоих источников без петель и перехватов.
    ///
    /// <b>Event-driven обработка.</b> Подписка на <c>ScreenPosition.OnUpdate</c> и
    /// <c>Actions.OnUpdate</c> ставит <see cref="_dirty"/>-флаг; <see cref="LateUpdate"/>
    /// обрабатывает цикл только при <c>_dirty = true</c> и делает это один раз на кадр
    /// независимо от числа апдейтов реактива (стик может репортить N раз за кадр —
    /// получим один raycast). В простое (курсор стоит, маска не меняется) стоимость —
    /// две проверки флага.
    ///
    /// <b>Ленивая инициализация.</b> Диспетчер работает, даже когда положен на
    /// persistent-сцену (Preload), а <see cref="EventSystem"/> живёт на UI-сцене,
    /// которая грузится позже. Инициализация откладывается до момента, когда и
    /// <c>EventSystem.current</c>, и <see cref="VirtualCursorBus.IsReady"/> стали true.
    ///
    /// <b>Поддерживаемые события.</b> Enter/Exit (hover), Down/Up/Click для
    /// <see cref="PointerAction.Action1"/>/<see cref="PointerAction.Action2"/>/<see cref="PointerAction.Action3"/>
    /// (LMB/RMB/MMB по конвенции <see cref="PointerAction"/>). Drag/Scroll/Submit/Navigation —
    /// не реализованы; слоты в маске зарезервированы под Scroll
    /// (<see cref="PointerAction.Action6"/>/<see cref="PointerAction.Action7"/>).
    ///
    /// <b>Монтаж.</b> Один экземпляр на persistent-сцене рядом с <see cref="VirtualCursorBootstrap"/>
    /// и <see cref="UiImageCursorRenderer"/>. При <see cref="OnDisable"/> снимает все зависшие
    /// Enter/Press — чтобы следующий сценарий не унаследовал подвисший hover.
    /// </summary>
    public class VirtualPointerDispatcher : MonoBehaviour
    {
        // PointerAction.None..Action10 (включительно).
        private const int ActionSlotCount = 11;

        private readonly List<RaycastResult> _raycastBuffer = new();
        private readonly bool[] _wasActive = new bool[ActionSlotCount];
        private readonly GameObject[] _pressTargets = new GameObject[ActionSlotCount];

        private GameObject _lastHover;
        private PointerEventData _ped;
        private bool _subscribed;
        private bool _dirty;

        private void OnEnable()
        {
            TrySubscribeBus();
            VirtualCursorBus.OnReady += TrySubscribeBus;
        }

        private void OnDisable()
        {
            VirtualCursorBus.OnReady -= TrySubscribeBus;
            UnsubscribeBus();
            ClearPointerState();
            _ped = null;
        }

        /// <summary>
        /// Подписка на реактивы Bus — безопасна при повторных вызовах и при отсутствии данных.
        /// Вызывается на <see cref="OnEnable"/> и на событии <c>OnReady</c>; при initial Enable
        /// до инициализации контроллера сработает ещё раз, когда Bus станет готов.
        /// </summary>
        private void TrySubscribeBus()
        {
            if (_subscribed || !VirtualCursorBus.IsReady) return;
            var data = VirtualCursorBus.Data;
            if (data == null) return;

            data.ScreenPosition.OnUpdate += OnPositionChanged;
            data.Actions.OnUpdate += OnActionsChanged;
            _subscribed = true;
            // Первая отрисовка: позиция/маска уже лежат в Bus, но событий ещё не было
            // с момента нашей подписки — помечаем dirty, чтобы ближайший LateUpdate
            // обсчитал стартовый hover-state.
            _dirty = true;
        }

        private void UnsubscribeBus()
        {
            if (!_subscribed) return;
            var data = VirtualCursorBus.Data;
            if (data != null)
            {
                data.ScreenPosition.OnUpdate -= OnPositionChanged;
                data.Actions.OnUpdate -= OnActionsChanged;
            }
            _subscribed = false;
        }

        private void OnPositionChanged(Vector2 _) => _dirty = true;
        private void OnActionsChanged(PointerActionMask _) => _dirty = true;

        private void LateUpdate()
        {
            // Фаст-пас в простое: ни позиция, ни маска не менялись → работы ноль.
            if (!_dirty) return;

            var es = EventSystem.current;
            var data = VirtualCursorBus.Data;
            if (es == null || data == null) return; // EventSystem может прийти позже с другой сцены

            // Ленивый PointerEventData — EventSystem до этого мог быть null (Preload до загрузки UI).
            _ped ??= new PointerEventData(es);

            _dirty = false;

            var pos = data.ScreenPosition.Value;
            var mask = data.Actions.Value;

            // position/delta/pointerCurrentRaycast — пересчитываются каждый прогон;
            // pointerPress/pressPosition хранятся между кадрами для корректного PointerClick,
            // поэтому полный PointerEventData.Reset() намеренно не используем.
            _ped.position = pos;
            _ped.delta = Vector2.zero;
            _ped.pointerCurrentRaycast = default;

            _raycastBuffer.Clear();
            es.RaycastAll(_ped, _raycastBuffer);
            var topRaycast = _raycastBuffer.Count > 0 ? _raycastBuffer[0] : default;
            _ped.pointerCurrentRaycast = topRaycast;
            var current = topRaycast.gameObject;

            // Enter/Exit — пройти ExecuteHierarchy по target'у и его предкам, чтобы Button-highlight
            // и прочие IPointerEnterHandler-ы на дочерних элементах срабатывали корректно.
            if (current != _lastHover)
            {
                if (_lastHover != null)
                    ExecuteEvents.ExecuteHierarchy(_lastHover, _ped, ExecuteEvents.pointerExitHandler);
                if (current != null)
                    ExecuteEvents.ExecuteHierarchy(current, _ped, ExecuteEvents.pointerEnterHandler);
                _lastHover = current;
            }

            // Down/Up/Click для LMB/RMB/MMB. Клик считается только если up произошёл на том же
            // IPointerClickHandler-таргете, что и down (стандартный UGUI-канон).
            HandleAction(PointerAction.Action1, PointerEventData.InputButton.Left, mask, current, topRaycast);
            HandleAction(PointerAction.Action2, PointerEventData.InputButton.Right, mask, current, topRaycast);
            HandleAction(PointerAction.Action3, PointerEventData.InputButton.Middle, mask, current, topRaycast);
        }

        private void HandleAction(PointerAction action, PointerEventData.InputButton button,
            PointerActionMask mask, GameObject current, RaycastResult topRaycast)
        {
            var idx = (int)action;
            var isActive = mask.IsActive(action);
            var wasActive = _wasActive[idx];
            if (isActive == wasActive) return;
            _wasActive[idx] = isActive;

            _ped.button = button;

            if (isActive)
            {
                // Rising edge → PointerDown. ExecuteHierarchy ищет IPointerDownHandler вверх
                // по parent-цепочке; если его нет — target берётся как ближайший IPointerClickHandler
                // (стандартный fallback UGUI, повторяет StandaloneInputModule).
                _ped.pressPosition = _ped.position;
                _ped.pointerPressRaycast = topRaycast;
                _ped.rawPointerPress = current;

                var pressTarget = ExecuteEvents.ExecuteHierarchy(current, _ped, ExecuteEvents.pointerDownHandler)
                                  ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);
                _ped.pointerPress = pressTarget;
                _pressTargets[idx] = pressTarget;
            }
            else
            {
                // Falling edge → PointerUp + (условно) PointerClick.
                var pressTarget = _pressTargets[idx];
                _pressTargets[idx] = null;
                if (pressTarget == null) return;

                ExecuteEvents.Execute(pressTarget, _ped, ExecuteEvents.pointerUpHandler);
                var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);
                if (clickTarget == pressTarget)
                    ExecuteEvents.Execute(clickTarget, _ped, ExecuteEvents.pointerClickHandler);
            }
        }

        /// <summary>
        /// Снять зависшие Enter/Press на отключении компонента. Иначе следующий сценарий
        /// (переоткрытие сцены / повторный Enable) унаследует подвисший hover-state и
        /// pressed-кнопки на уничтоженных или новых объектах — утечка пойдёт в highlights
        /// и clicks (которые будут падать на новом объекте без предыдущего Down).
        /// </summary>
        private void ClearPointerState()
        {
            if (_ped == null) return;

            if (_lastHover != null)
            {
                ExecuteEvents.ExecuteHierarchy(_lastHover, _ped, ExecuteEvents.pointerExitHandler);
                _lastHover = null;
            }

            for (var i = 0; i < ActionSlotCount; i++)
            {
                var pressTarget = _pressTargets[i];
                if (pressTarget != null)
                {
                    _ped.button = ButtonForSlot(i);
                    ExecuteEvents.Execute(pressTarget, _ped, ExecuteEvents.pointerUpHandler);
                    _pressTargets[i] = null;
                }

                _wasActive[i] = false;
            }
        }

        private static PointerEventData.InputButton ButtonForSlot(int slot) => slot switch
        {
            (int)PointerAction.Action1 => PointerEventData.InputButton.Left,
            (int)PointerAction.Action2 => PointerEventData.InputButton.Right,
            _ => PointerEventData.InputButton.Middle,
        };
    }
}
#endif
