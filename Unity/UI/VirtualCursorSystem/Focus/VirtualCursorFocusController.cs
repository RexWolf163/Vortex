using UnityEngine;
using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Статический контроллер подсистемы фокус-навигации виртуального курсора.
    /// Владеет <see cref="FocusModel"/>, принимает push/pop <see cref="FocusGroup"/>-ов
    /// в LIFO-стек (через их <c>OnEnable</c>/<c>OnDisable</c>), обрабатывает
    /// <see cref="Navigate"/> от <c>UINavigationDriver</c>.
    ///
    /// <b>Активная группа.</b> <see cref="FocusModel.ActiveGroup"/> — верхняя в стеке.
    /// Только её <c>Targets</c> участвуют в Navigate. Переключение активной группы
    /// (push/pop) — через стандартный Unity-паттерн SetActive меню/HUD.
    ///
    /// <b>Алгоритм Navigate.</b> Полуоткрытый конус <c>[-cone..+cone)</c> от позиции
    /// курсора — target попадает ровно в одну зону при покрытии 360° четырьмя
    /// направлениями. Среди попавших — минимум евклидовой дистанции. Текущий
    /// <see cref="FocusModel.CurrentFocus"/> исключается из кандидатов.
    ///
    /// <b>Авто-передача фокуса на push/pop.</b> Срабатывает только когда
    /// <see cref="_focusAnchor"/> != null — т.е. пользователь сейчас в nav mode
    /// (курсор «прибит» к target'у, не двигался мышью). При push новой группы:
    /// её <see cref="FocusGroup.RememberedFocus"/> или
    /// <see cref="FocusGroup.FindNearestEuclidean"/> становится новым фокусом,
    /// курсор warp'ается, HideCursor остаётся активным. При pop — симметрично:
    /// новая активная (нижняя по стеку) восстанавливает свой RememberedFocus
    /// или ближайший к курсору. Если <see cref="_focusAnchor"/> == null —
    /// автоматика выключена, стек просто перестраивается.
    ///
    /// <b>Авто-ClearFocus на движении курсора.</b> При успешном переходе фокуса
    /// запоминаем якорь-позицию. Подписка на <c>ScreenPosition.OnUpdate</c>: если
    /// delta &gt; <see cref="AnchorToleranceSqr"/> — пользователь двинул курсор →
    /// <see cref="ClearFocus"/>. Warp-эхо попадает в tolerance и игнорируется.
    /// </summary>
    public static class VirtualCursorFocusController
    {
        private static readonly object Key = new();

        // 3 px — укладывает hardware noise ОС-мыши и int-округление WarpCursorPosition
        // в одном кадре. Якорь при этом абсорбирует дрейф (см. OnScreenPositionChanged),
        // поэтому порог per-frame, а не "всего накопилось от момента SetFocus".
        private const float AnchorToleranceSqr = 9f;

        private static FocusModel _model;

        /// <summary>Runtime-реестр фокус-навигации. <c>null</c> до <see cref="Init"/>.</summary>
        public static FocusModel Model => _model;

        public static bool IsReady { get; private set; }

        // Якорь текущего фокуса; null = курсор не "прибит" (режим свободного курсора).
        // Критично для авто-передачи фокуса на push/pop и для различения warp-эхо.
        private static Vector2? _focusAnchor;

        // Запомненные флаги последнего Navigate — применяются при автоматических
        // переходах фокуса (push/pop группы), чтобы использовать те же параметры
        // hide/warp, с которыми пользователь зашёл в nav mode.
        private static bool _lastHideCursor;
        private static bool _lastWarpSystemMouse;

        public static void Init()
        {
            if (IsReady) return;

            _model = new FocusModel();
            _model.SetOwner(Key);

            if (VirtualCursorBus.Data != null)
                VirtualCursorBus.Data.ScreenPosition.OnUpdate += OnScreenPositionChanged;
            else
                VirtualCursorBus.OnReady += AttachPositionWatcher;

            IsReady = true;
        }

        private static void AttachPositionWatcher()
        {
            VirtualCursorBus.OnReady -= AttachPositionWatcher;
            if (VirtualCursorBus.Data != null)
                VirtualCursorBus.Data.ScreenPosition.OnUpdate += OnScreenPositionChanged;
        }

        public static void Cleanup()
        {
            if (!IsReady) return;
            if (VirtualCursorBus.Data != null)
                VirtualCursorBus.Data.ScreenPosition.OnUpdate -= OnScreenPositionChanged;
            VirtualCursorBus.OnReady -= AttachPositionWatcher;
            _focusAnchor = null;
            _model = null;
            IsReady = false;
        }

        /// <summary>
        /// Push группы в LIFO-стек. Зовётся <see cref="FocusGroup"/>.OnEnable.
        /// Если курсор в nav mode (_focusAnchor != null) — активный фокус старой
        /// группы сохраняется в её RememberedFocus и получает NotifyUnfocused;
        /// в новой группе выбирается RememberedFocus или ближайший к курсору,
        /// на него переносится фокус. Иначе — только обновление стека.
        /// </summary>
        public static void PushGroup(FocusGroup group)
        {
            if (_model == null || group == null) return;

            // Старая активная группа — её фокус (если был) переносится в её RememberedFocus
            // и получает NotifyUnfocused: группа теряет top-статус, визуально фокус с её
            // target'а должен сняться.
            TransferCurrentToRemembered();

            _model.PushGroup(group);
            TryAutoFocusNewActiveGroup();
        }

        /// <summary>
        /// Снять группу из LIFO. Зовётся <see cref="FocusGroup"/>.OnDisable или OnDestroy.
        /// Если это была активная группа — старый фокус (её RememberedFocus обновлён)
        /// снимается, затем новая активная (следующая вниз по стеку) восстанавливает
        /// свой RememberedFocus или ближайший — при условии nav mode.
        /// </summary>
        public static void RemoveGroup(FocusGroup group)
        {
            if (_model == null || group == null) return;

            TransferCurrentToRemembered();

            _model.RemoveGroup(group);
            TryAutoFocusNewActiveGroup();
        }

        /// <summary>
        /// Переместить фокус в направлении <paramref name="direction"/> в активной группе.
        /// Критерии кандидата: активен, не текущий фокус, угол к нему от направления
        /// ∈ <c>[-cone..+cone)</c>. Среди кандидатов — минимум евклидовой дистанции.
        ///
        /// <paramref name="captureNearestIfFree"/>: fallback-режим «захвата фокуса».
        /// Если в конусе нет кандидатов И текущий фокус пуст (курсор свободен, нет
        /// nav-mode) — выбираем ближайший активный target группы по евклидовой
        /// дистанции от курсора, без учёта направления. Любая клавиша-направление
        /// в таком случае становится точкой входа в nav-mode: курсор «прилипает»
        /// к ближайшей кнопке. При уже активном фокусе fallback не срабатывает —
        /// иначе навигация у края группы (нет соседей в этом направлении) молча
        /// бы перескакивала на произвольный элемент, что путает пользователя.
        /// </summary>
        public static void Navigate(Vector2 direction, float coneAngleDeg,
            bool hideCursor, bool warpSystemMouse, bool captureNearestIfFree)
        {
            if (_model == null) return;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (VirtualCursorBus.Data == null) return;

            var activeGroup = _model.ActiveGroup;
            if (activeGroup == null) return;

            var current = _model.CurrentFocus.Value;
            // Origin — точная ScreenPoint текущего фокуса, а НЕ позиция курсора. Курсор
            // после WarpCursorPosition округляется ОС-мышью до целого пикселя (API принимает
            // int), а потом его позиция приходит обратно в ScreenPosition с echo-поправкой.
            // Из-за этого origin съезжает на 0.3–0.7 пикс от истинного центра current.
            // На коротких дистанциях / узких конусах это ломает ранжирование: кандидат
            // в выборке получает угол, выходящий за [-cone..+cone), либо distSqr-сортировка
            // путает порядок (ties). ScreenPoint — живой float от RectTransformUtility,
            // всегда точный. Если current нет (ClearFocus) — origin от курсора, других
            // ориентиров нет.
            var origin = current != null
                ? current.ScreenPoint
                : VirtualCursorBus.Data.ScreenPosition.Value;
            var dir = direction.normalized;
            var cone = Mathf.Abs(coneAngleDeg);

            IFocusTarget best = null;
            var bestDistSqr = float.MaxValue;

            var targets = activeGroup.Targets;
            for (var i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsActive) continue;
                // Явное исключение текущего — даже при origin == current.ScreenPoint
                // defensive-ный ReferenceEquals ловит случаи, где distSqr мог бы оказаться
                // ненулевым из-за реализации ScreenPoint (например, пересчёт между двумя
                // чтениями подряд даёт разные значения на долях пикселя).
                if (ReferenceEquals(t, current)) continue;

                var vec = t.ScreenPoint - origin;
                var distSqr = vec.sqrMagnitude;
                if (distSqr < 0.0001f) continue;

                var angle = Vector2.SignedAngle(dir, vec);
                if (angle < -cone || angle >= cone) continue;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = t;
                }
            }

            if (best == null)
            {
                // Fallback «захват фокуса»: курсор свободен, в конусе никого — берём
                // ближайший активный элемент группы без учёта направления. Для уже
                // активного фокуса этот fallback НЕ применяем (см. контракт выше).
                if (captureNearestIfFree && current == null)
                {
                    var cursorPos = VirtualCursorBus.Data.ScreenPosition.Value;
                    best = activeGroup.FindNearestEuclidean(cursorPos);
                }
                if (best == null) return;
            }

            // Запомнили флаги — пригодятся при auto-focus на push/pop.
            _lastHideCursor = hideCursor;
            _lastWarpSystemMouse = warpSystemMouse;

            SetFocusInternal(best, hideCursor, warpSystemMouse);
        }

        /// <summary>Явный сброс фокуса. Если был текущий target — <c>NotifyUnfocused</c>.</summary>
        public static void ClearFocus()
        {
            if (_model == null) return;
            var current = _model.CurrentFocus.Value;
            if (current == null) return;

            // Запомнить фокус в активной группе перед сбросом — чтобы при возврате
            // в nav mode (например, pop родительской группы над ней) можно было вернуться.
            var activeGroup = _model.ActiveGroup;
            if (activeGroup != null) activeGroup.RememberedFocus = current;

            _model.SetCurrent(null, Key);
            _focusAnchor = null;
            VirtualCursorController.ShowCursor();
            current.NotifyUnfocused();
        }

        /// <summary>
        /// Снять текущий фокус (если был) и запомнить его в активной группе как
        /// RememberedFocus — для последующего auto-restore. Не трогает _focusAnchor
        /// (nav mode state), ShowCursor, _lastHide/Warp — чтобы следующий
        /// auto-focus мог продолжить в том же режиме.
        /// </summary>
        private static void TransferCurrentToRemembered()
        {
            if (_model == null) return;
            var current = _model.CurrentFocus.Value;
            if (current == null) return;

            var activeGroup = _model.ActiveGroup;
            if (activeGroup != null) activeGroup.RememberedFocus = current;

            _model.SetCurrent(null, Key);
            current.NotifyUnfocused();
        }

        /// <summary>
        /// Авто-передача фокуса в новой активной группе. Только если курсор в nav mode
        /// (_focusAnchor != null) — т.е. фокус-навигация активна. Target берётся из
        /// RememberedFocus группы (приоритет), иначе — ближайший евклидовой дистанции.
        /// </summary>
        private static void TryAutoFocusNewActiveGroup()
        {
            if (_model == null) return;
            if (!_focusAnchor.HasValue) return;             // Пользователь не в nav mode — пропускаем
            if (VirtualCursorBus.Data == null) return;

            var activeGroup = _model.ActiveGroup;
            if (activeGroup == null)
            {
                // Стек пуст (всё закрылось) — выйти из nav mode аккуратно.
                _focusAnchor = null;
                VirtualCursorController.ShowCursor();
                return;
            }

            var cursorPos = VirtualCursorBus.Data.ScreenPosition.Value;
            // Remembered имеет приоритет, если ещё активен; иначе ближайший.
            var target = activeGroup.RememberedFocus;
            if (target == null || !target.IsActive)
                target = activeGroup.FindNearestEuclidean(cursorPos);

            if (target == null)
            {
                // В новой группе никого — остаёмся в nav mode без фокуса. Пользователь
                // двинет курсор или нажмёт направление и ситуация разрулится по месту.
                return;
            }

            SetFocusInternal(target, _lastHideCursor, _lastWarpSystemMouse);
        }

        private static void SetFocusInternal(IFocusTarget target, bool hideCursor, bool warpSystemMouse)
        {
            var old = _model.CurrentFocus.Value;
            if (ReferenceEquals(old, target)) return;

            var pos = target.ScreenPoint;
            _focusAnchor = pos;
            _lastHideCursor = hideCursor;
            _lastWarpSystemMouse = warpSystemMouse;

            _model.SetCurrent(target, Key);
            // old здесь обычно null (TransferCurrentToRemembered уже снял) — но для Navigate
            // старый target остаётся и его нужно уведомить + запомнить в активной группе.
            if (old != null)
            {
                var activeGroup = _model.ActiveGroup;
                if (activeGroup != null) activeGroup.RememberedFocus = old;
                old.NotifyUnfocused();
            }
            target.NotifyFocused();

            // Запомним и в активной группе — на случай переключения группы без Navigate.
            {
                var activeGroup = _model.ActiveGroup;
                if (activeGroup != null) activeGroup.RememberedFocus = target;
            }

            if (hideCursor)
                VirtualCursorController.HideCursor();

            VirtualCursorController.ReportPointer(pos, PointerSourceKind.Direct);

            if (warpSystemMouse)
                Mouse.current?.WarpCursorPosition(pos);
        }

        private static void OnScreenPositionChanged(Vector2 newPos)
        {
            if (!_focusAnchor.HasValue) return;
            var delta = newPos - _focusAnchor.Value;
            if (delta.sqrMagnitude < AnchorToleranceSqr)
            {
                // Поглощаем микро-дрейф в сам anchor. Если держать anchor фиксированным
                // (как было раньше), hardware noise ОС-мыши (~1 px/кадр при простое) и
                // int-округление WarpCursorPosition (ещё ~0.5 px) накапливаются кадр за
                // кадром: ни один одиночный кадр не несёт движения, но суммарная дельта
                // от якоря через секунду-другую пересекает tolerance и ломает nav-mode
                // без действия пользователя — выглядит как «периодический выброс в
                // свободный курсор». С абсорбцией сравнивается всегда дельта одного
                // кадра: намеренный жест мышью (5+ px/кадр даже на самом медленном
                // осознанном движении на 60 Гц) всё равно пробивает порог.
                _focusAnchor = newPos;
                return;
            }

            // Пользователь двинул курсор — выход из nav mode: snятие фокуса, snятие HideCursor.
            ClearFocus();
        }
    }
}
