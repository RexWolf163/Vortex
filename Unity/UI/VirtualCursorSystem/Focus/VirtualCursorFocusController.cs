using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;

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
        private static readonly object FollowOwner = new();

        // 3 px — укладывает hardware noise ОС-мыши и int-округление WarpCursorPosition
        // в одном кадре. Якорь при этом абсорбирует дрейф (см. OnScreenPositionChanged),
        // поэтому порог per-frame, а не "всего накопилось от момента SetFocus".
        private const float AnchorToleranceSqr = 9f;

        // Порог сопровождения — минимальное движение target'а в пикс², которое вызывает
        // переноc курсора. Чуть меньше AnchorToleranceSqr, чтобы follow-tick реагировал на
        // ScrollRect-смещение раньше, чем OnScreenPositionChanged мог бы ошибочно сработать
        // от того же движения. Экономия на no-op кадрах (target не движется).
        private const float FollowThresholdSqr = 0.25f; // 0.5 px

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

        // Re-entry guard: _busy выставлен ТОЛЬКО на время вызова пользовательских onFocused/onUnfocused.
        // Любая мутация фокуса/стека групп из такого обработчика отклоняется fail-loud — иначе
        // незащищённая рекурсия Notify→Navigate/Push/Ignore→Notify даёт StackOverflow.
        private static bool _busy;

        public static void Init()
        {
            if (IsReady) return;

            _model = new FocusModel();
            _model.SetOwner(Key);

            if (VirtualCursorBus.Data != null)
                VirtualCursorBus.Data.ScreenPosition.OnUpdate += OnScreenPositionChanged;
            else
                VirtualCursorBus.OnReady += AttachPositionWatcher;

            // Follow-петля: самоперепланирующаяся через TimeController.Accumulate (как у драйверов
            // ввода). На каждом кадре сверяет ScreenPoint текущего target'а с якорем и, если тот
            // физически сместился (ScrollRect, layout group, анимация, DOTween на RectTransform), —
            // переносит курсор следом (ReportPointer + warp). Это важно, когда FocusCenterScrollRect
            // прокручивает контент под фокусным элементом: без follow'а курсор остался бы в точке
            // прежнего warp'а, визуально оторвавшись от элемента.
            TimeController.Accumulate(FollowFocus, FollowOwner);

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
            TimeController.RemoveCall(FollowOwner);
            _focusAnchor = null;
            _busy = false;
            _model = null;
            IsReady = false;
        }

        // true (+ LogError), если вызвано реентерабельно из обработчика фокуса — вызывающий выходит.
        private static bool Reenter()
        {
            if (!_busy) return false;
            Debug.LogError("[VirtualCursorFocus] Реентерабельный вызов из обработчика фокуса " +
                           "(onFocused/onUnfocused) — игнорирован. Эти события не должны менять фокус " +
                           "или стек групп.");
            return true;
        }

        // Вызов пользовательского UnityEvent под _busy — окно, в котором мутации фокуса отклоняются.
        private static void Notify(IFocusTarget target, bool focused)
        {
            _busy = true;
            try
            {
                if (focused) target.NotifyFocused();
                else target.NotifyUnfocused();
            }
            finally { _busy = false; }
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
            if (Reenter()) return;

            // Гард фликера: двигаем фокус только если push реально сменил активную группу.
            // Push группы с меньшим приоритетом / Ignored оставляет ActiveGroup прежней — тогда
            // Transfer+auto-focus сняли бы и тут же вернули фокус на ТОТ ЖЕ target (лишние
            // NotifyUnfocused/NotifyFocused + повторный Warp). Симметрично гарду OnGroupIgnoreChanged.
            var before = _model.ActiveGroup;
            _model.PushGroup(group);
            if (ReferenceEquals(_model.ActiveGroup, before)) return;

            // Активная группа сменилась: фокус старой (если был) уходит в её RememberedFocus +
            // NotifyUnfocused (старая группа ещё в стеке — FindOwningGroup её найдёт), новая
            // активная получает фокус.
            TransferCurrentToRemembered();
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
            if (Reenter()) return;

            // Гард фликера: если снимаемая группа не активна, ActiveGroup не изменится —
            // перефокусировать не нужно (неактивная группа свой фокус уже отдала в Remembered
            // при потере top-статуса).
            var wasActive = ReferenceEquals(_model.ActiveGroup, group);

            // Transfer ДО удаления: фокус активной группы сохраняется в её же RememberedFocus
            // (FindOwningGroup найдёт её, пока она ещё в стеке).
            if (wasActive)
                TransferCurrentToRemembered();

            _model.RemoveGroup(group);

            if (wasActive)
                TryAutoFocusNewActiveGroup();
        }

        /// <summary>
        /// Реакция на уход target'а из системы (его <see cref="FocusGroup.Unregister"/> — обычно
        /// <c>FocusTargetComponent.OnDisable</c>). Если уходящий target был текущим фокусом, снимаем
        /// висящий <c>CurrentFocus</c> (он теперь указывает на выбывший элемент) + <c>NotifyUnfocused</c>.
        /// <b>Nav-mode НЕ трогаем</b> (не сбрасываем <see cref="_focusAnchor"/>, не зовём ShowCursor):
        /// если следом идёт teardown группы (закрытие меню), <see cref="TryAutoFocusNewActiveGroup"/>
        /// перенесёт фокус на группу под ней; если ничего не следует — ближайшее движение курсора
        /// (<see cref="ClearFocus"/>) или <see cref="Navigate"/> разрулят. Без этого модель до
        /// следующего Navigate держала бы ссылку на отключённый target.
        /// </summary>
        internal static void OnTargetUnregistered(IFocusTarget target)
        {
            if (_model == null || target == null) return;
            if (Reenter()) return;
            if (!ReferenceEquals(_model.CurrentFocus.Value, target)) return;
            _model.SetCurrent(null, Key);
            Notify(target, false);
        }

        /// <summary>
        /// Реакция на runtime-смену <see cref="FocusGroup.Ignore"/>. Зовётся из setter'а
        /// свойства самой группы. Если смена Ignore повлияла на <see cref="FocusModel.ActiveGroup"/>
        /// (её либо скрыло, либо наоборот — вернуло более высокоприоритетную в игру),
        /// корректно переносит фокус: NotifyUnfocused на старом target'е, запись в
        /// RememberedFocus владеющей группы, auto-focus в новой активной (при nav mode).
        ///
        /// Если смена Ignore на ActiveGroup не влияет (например, Ignore'нули группу
        /// в глубине стека, которая и так не была активной) — ничего не делаем, фликера
        /// фокуса нет.
        /// </summary>
        internal static void OnGroupIgnoreChanged(FocusGroup group)
        {
            if (_model == null || group == null) return;
            if (Reenter()) return;

            var current = _model.CurrentFocus.Value;
            if (current == null) return;

            // Если текущий фокус всё ещё принадлежит активной группе — ничего не менялось
            // для него (Ignore затронул чужой слой стека). Пропускаем, чтобы не было фликера.
            var activeGroup = _model.ActiveGroup;
            if (activeGroup != null && ContainsTarget(activeGroup, current)) return;

            // CurrentFocus теперь вне активной группы: либо его группа только что стала
            // Ignored, либо выше в стеке unIgnored'нулась другая, которая его перекрыла.
            // Переносим: Remembered в владеющую группу (сохраняется для будущего возврата)
            // + NotifyUnfocused (визуал снимается немедленно). Затем auto-focus в новой
            // активной — только если юзер в nav mode (_focusAnchor != null); иначе остаёмся
            // без фокуса, пока пользователь не нажмёт направление.
            TransferCurrentToRemembered();
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
        ///
        /// Stale-current (принадлежит Ignored/не-ActiveGroup группе): трактуется как
        /// null для целей ЭТОГО Navigate — origin от курсора, исключение не нужно
        /// (его всё равно нет в активной Targets), captureIfFree может сработать.
        /// Группа-владелец stale-current свой RememberedFocus не теряет (мы его тут
        /// не трогаем). Это необходимо для сценария «пользователь выставил Ignore
        /// на фокусной группе в рантайме» — иначе Navigate в соседних группах
        /// работал бы странно из-за «залипшего» CurrentFocus.
        /// </summary>
        public static void Navigate(Vector2 direction, float coneAngleDeg,
            bool hideCursor, bool warpSystemMouse, bool captureNearestIfFree)
        {
            if (_model == null) return;
            if (Reenter()) return;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (VirtualCursorBus.Data == null) return;

            var activeGroup = _model.ActiveGroup;
            if (activeGroup == null) return;

            var current = _model.CurrentFocus.Value;
            // Если current принадлежит не activeGroup (Ignored соседка или stale после
            // ручной мутации стека) — для текущего Navigate это шум. Обнуляем локально:
            // origin съедет на cursorPos, captureIfFree сможет сработать, exclusion не нужен
            // (current в activeGroup.Targets отсутствует и так).
            if (current != null && !ContainsTarget(activeGroup, current))
                current = null;

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

        /// <summary>
        /// Явный сброс фокуса + выход из nav-mode. Делает две независимые работы:
        /// <list type="bullet">
        /// <item>Если был <c>CurrentFocus</c> — сохраняет его в <c>RememberedFocus</c>
        /// владеющей группы, снимает (<c>SetCurrent(null)</c>), зовёт <c>NotifyUnfocused</c>.</item>
        /// <item>Независимо от этого — если установлен <see cref="_focusAnchor"/>, обнуляет его
        /// и зовёт <c>ShowCursor()</c>, чтобы снять внешний канал скрытия (`_externalHidden`).</item>
        /// </list>
        ///
        /// Это важно для патового состояния: PushGroup с пустой новой группой (Unity вызывает
        /// OnEnable родителя ДО детей, поэтому <c>Group.Targets</c> пуст в момент push'а → auto-focus
        /// не находит таргета → <c>CurrentFocus=null</c> из <c>TransferCurrentToRemembered</c>, но
        /// <see cref="_focusAnchor"/> и <c>_externalHidden</c> остаются «висеть». Без второй ветки
        /// `ClearFocus` в такой момент выходил бы по `current==null` раньше, чем снимал курсор, —
        /// и пользователь оказывался в заклиненном nav-mode без видимого курсора.
        /// </summary>
        public static void ClearFocus()
        {
            if (_model == null) return;
            if (Reenter()) return;

            var current = _model.CurrentFocus.Value;
            if (current != null)
            {
                // Remembered пишем в ВЛАДЕЮЩУЮ группу target'а, не в ActiveGroup. Иначе если
                // current принадлежит теперь-Ignored или stale-группе (пользователь выставил
                // Ignore в рантайме), запись в ActiveGroup засорила бы её Remembered чужим
                // элементом из другой контекстной группы.
                var owning = FindOwningGroup(current);
                if (owning != null) owning.RememberedFocus = current;

                _model.SetCurrent(null, Key);
                Notify(current, false);
            }

            // Nav-mode state чистится всегда, даже когда current был уже null, — иначе
            // «застрявший» _focusAnchor + _externalHidden никак не снимаются с внешней стороны.
            if (_focusAnchor.HasValue)
            {
                _focusAnchor = null;
                VirtualCursorController.ShowCursor();
            }
        }

        /// <summary>
        /// Снять текущий фокус (если был) и запомнить его в его ВЛАДЕЮЩЕЙ группе как
        /// RememberedFocus (не обязательно ActiveGroup — current может быть stale, см.
        /// <see cref="ClearFocus"/>). Не трогает <see cref="_focusAnchor"/> (nav-mode state),
        /// ShowCursor, _lastHide/Warp — следующий auto-focus продолжит в том же режиме.
        /// </summary>
        private static void TransferCurrentToRemembered()
        {
            if (_model == null) return;
            var current = _model.CurrentFocus.Value;
            if (current == null) return;

            var owning = FindOwningGroup(current);
            if (owning != null) owning.RememberedFocus = current;

            _model.SetCurrent(null, Key);
            Notify(current, false);
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
            // fake-null: Remembered мог быть уничтожен ПОСЛЕ того, как пережил Unregister (он теперь
            // чистится только при реальном destroy — см. FocusGroup.Unregister). Ссылочное == null
            // уничтоженный Unity-объект не ловит, а .IsActive бросил бы MissingReferenceException.
            if (target == null || target is Object o && o == null || !target.IsActive)
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
            // старый target остаётся и его нужно уведомить + запомнить в его ВЛАДЕЮЩЕЙ
            // группе (не обязательно ActiveGroup — old мог прийти из stale/Ignored группы).
            if (old != null)
            {
                var oldOwning = FindOwningGroup(old);
                if (oldOwning != null) oldOwning.RememberedFocus = old;
                Notify(old, false);
            }
            Notify(target, true);

            // Запомним в владеющей группе target'а. В штатном сценарии target приходит из
            // activeGroup.Targets, значит owning == activeGroup; но FindOwningGroup даёт
            // устойчивость к любому сценарию.
            var targetOwning = FindOwningGroup(target);
            if (targetOwning != null) targetOwning.RememberedFocus = target;

            if (hideCursor)
                VirtualCursorController.HideCursor();

            VirtualCursorController.ReportPointer(pos, PointerSourceKind.Direct);

            if (warpSystemMouse)
                Mouse.current?.WarpCursorPosition(pos);
        }

        /// <summary>
        /// Найти группу, в <c>Targets</c> которой зарегистрирован этот target. Нужно, чтобы
        /// корректно писать <c>RememberedFocus</c> в его ВЛАДЕЮЩУЮ группу (а не в ActiveGroup,
        /// которая может быть другой при stale/Ignored раскладе). O(groups × targets_per_group)
        /// линейный поиск — для типичных сцен (≤5 групп × ≤20 target'ов) незаметно.
        /// Возвращает <c>null</c>, если target не найден ни в одной группе (например, был
        /// Unregister'ен после попадания в <c>CurrentFocus</c>).
        /// </summary>
        private static FocusGroup FindOwningGroup(IFocusTarget target)
        {
            if (_model == null || target == null) return null;
            var groups = _model.Groups;
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g != null && ContainsTarget(g, target)) return g;
            }
            return null;
        }

        /// <summary>Содержит ли группа этот target в своём списке <c>Targets</c>.</summary>
        private static bool ContainsTarget(FocusGroup group, IFocusTarget target)
        {
            if (group == null || target == null) return false;
            var ts = group.Targets;
            for (var i = 0; i < ts.Count; i++)
            {
                if (ReferenceEquals(ts[i], target)) return true;
            }
            return false;
        }

        /// <summary>
        /// Follow-петля: сопровождение курсора за физически смещающимся target'ом. Запускается
        /// каждый кадр через <see cref="TimeController.Accumulate"/> (самоперепланирующаяся
        /// в finally — любой бросок в теле не рвёт цикл). Семантика:
        /// <list type="bullet">
        /// <item>Nav-mode выключен (<see cref="_focusAnchor"/>==null) → ничего не делаем.</item>
        /// <item>Нет current / target неактивен → ничего не делаем (ScreenPoint читать бессмысленно,
        /// да и warp бесполезен).</item>
        /// <item>target.ScreenPoint отличается от anchor на &gt;<see cref="FollowThresholdSqr"/> —
        /// переносим курсор: обновляем anchor (чтобы OnScreenPositionChanged абсорбировал
        /// собственный ReportPointer), шлём <c>ReportPointer</c> + warp ОС-мыши (с last-флагами,
        /// как при входе в nav-mode).</item>
        /// </list>
        /// Это закрывает кейс «ScrollRect прокрутил контент / layout group пересчитался /
        /// анимация передвинула target» — target движется БЕЗ собственных событий, но
        /// <c>IFocusTarget.ScreenPoint</c> у <see cref="FocusTargetComponent"/> пересчитывается
        /// при каждом запросе из live-RectTransform, так что polling даёт актуальную точку.
        /// </summary>
        private static void FollowFocus()
        {
            try { TickFollow(); }
            finally { TimeController.Accumulate(FollowFocus, FollowOwner); }
        }

        private static void TickFollow()
        {
            if (!_focusAnchor.HasValue) return;
            if (_model == null) return;
            var current = _model.CurrentFocus.Value;
            if (current == null || !current.IsActive) return;

            var newPos = current.ScreenPoint;
            if ((newPos - _focusAnchor.Value).sqrMagnitude < FollowThresholdSqr) return;

            // Anchor обновляем ДО ReportPointer — иначе OnScreenPositionChanged (который наш
            // собственный хук на ScreenPosition.OnUpdate) увидел бы большой delta от старого
            // якоря и сработал бы ClearFocus. С обновлённым якорем: delta=0 → absorb-ветка.
            _focusAnchor = newPos;
            VirtualCursorController.ReportPointer(newPos, PointerSourceKind.Direct);
            if (_lastWarpSystemMouse)
                Mouse.current?.WarpCursorPosition(newPos);
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
