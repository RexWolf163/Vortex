using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Controllers;
using Vortex.Unity.UI.VirtualCursorSystem.Handlers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Focus
{
    /// <summary>
    /// Статический контроллер подсистемы фокус-навигации виртуального курсора.
    ///
    /// <b>Активная группа.</b> <see cref="FocusModel.ActiveGroup"/> — верхняя в стеке, если не Ignore
    /// Только её <c>Targets</c> участвуют в Navigate. Переключение активной группы
    /// (push/pop) — через стандартный Unity-паттерн SetActive меню/HUD.
    ///
    /// <b>Алгоритм Navigate.</b> Полуоткрытый конус <c>[-cone..+cone)</c> от позиции
    /// курсора — target попадает ровно в одну зону при покрытии 360° четырьмя
    /// направлениями. Среди попавших — минимум евклидовой дистанции. Текущий
    /// <see cref="FocusModel.CurrentFocus"/> исключается из кандидатов.
    ///
    /// <b>Режим навигации.</b> Отдельный флаг <see cref="_navMode"/>: включается переходом фокуса, выключается
    /// только сдвигом курсора пользователем или опустевшим стеком групп. Переживает временную потерю фокуса
    /// (фокус ушёл в RememberedFocus при push, фокусная цель отключилась). Фокус ставится через
    /// <see cref="Navigate"/>; курсор переносится в точку цели (репорт позиции + варп ОС-мыши) и, по флагу, скрывается.
    ///
    /// <b>Авто-передача фокуса на push/pop.</b> При смене активной группы фокус старой уходит в её
    /// <see cref="FocusGroup.RememberedFocus"/>; новой активной передаётся её
    /// <see cref="FocusGroup.RememberedFocus"/> или <see cref="FocusGroup.FindNearestEuclidean"/> —
    /// только в режиме навигации, иначе стек просто перестраивается. Передача отложена до ближайшего тика петли
    /// сопровождения (<see cref="_pendingAutoFocus"/>): группа — предок своих целей, и на её OnEnable цели ещё
    /// не перерегистрированы; к тику все OnEnable пройдены, а смена меню в одном кадре (pop A + push B) не
    /// роняет режим навигации на промежуточном пустом стеке.
    ///
    /// <b>Выход из режима на движении курсора.</b> Подписка на <c>PointerModel.OnPointerMoved</c> — значимый сдвиг
    /// по фильтру дрейфа на входе репорта (VirtualCursorController.ReportPointer) (дрожание и эхо варпа ОС-мыши его не дают) завершает режим навигации и снимает фокус.
    /// Свои переносы курсора (переход фокуса, сопровождение) отсекаются флагом <see cref="_selfReporting"/>.
    /// Смещение самой цели фокус не снимает — его догоняет сопровождение.
    ///
    /// <b>Сопровождение.</b> Покадровая петля переносит курсор за целью, если её экранная точка сместилась
    /// (скролл, лейаут, анимация).
    /// </summary>
    public static class VirtualCursorFocusController
    {
        /// <summary>Ключ владельца реактивного поля фокуса.</summary>
        private static readonly object Key = new();

        /// <summary>Владелец вызова покадровой петли сопровождения в <see cref="TimeController"/>.</summary>
        private static readonly object FollowOwner = new();

        /// <summary>Модель фокуса шины, которой владеет контроллер.</summary>
        private static FocusModel _model;

        /// <summary>
        /// Допуск сопровождения, квадрат пикселей (2 px): расхождение точки цели и курсора меньше — курсор не переносится.
        /// Выше ошибки эха варпа ОС-мыши (округление до пикселя: ≈0.7 px, на HiDPI ≈1.4 px) — эхо, сдвинувшее курсор
        /// на долю пикселя, не вызывает повторный варп, и петля «эхо → сопровождение → варп» не возникает.
        /// </summary>
        private const float FollowToleranceSqr = 4f;

        /// <summary>
        /// Флаг скрытия курсора последнего перехода фокуса — переиспользуется автоматическими переходами
        /// (push/pop группы) и сопровождением, чтобы курсор оставался в том же режиме.
        /// </summary>
        private static bool _lastHideCursor;

        /// <summary>
        /// Окно уведомления: выставлен только на время вызова <see cref="IFocusTarget.NotifyFocused"/>/
        /// <see cref="IFocusTarget.NotifyUnfocused"/>. Смена фокуса из этих обработчиков (<see cref="Navigate"/>)
        /// отклоняется с ошибкой; изменения стека групп и регистрации целей (включение/выключение групп и целей,
        /// <see cref="FocusGroup.Ignore"/>) откладываются в <see cref="Deferred"/> — иначе рекурсия
        /// Notify → Push/Remove/Ignore → Notify, а отброшенный вызов оставил бы стек рассинхронизированным со сценой.
        /// </summary>
        private static bool _busy;

        /// <summary>
        /// Глубина вложенности операций контроллера (<see cref="BeginOperation"/>/<see cref="EndOperation"/>).
        /// Отложенные изменения выполняются, когда завершается внешняя операция, — не посреди перехода фокуса.
        /// </summary>
        private static int _operationDepth;

        /// <summary>
        /// Изменения стека групп и регистрации целей, пришедшие из обработчиков фокуса (<see cref="_busy"/>).
        /// Выполняются по порядку после завершения внешней операции (<see cref="EndOperation"/>); каждое
        /// перепроверяет состояние сцены на момент выполнения.
        /// </summary>
        private static readonly Queue<System.Action> Deferred = new();

        /// <summary>
        /// Выставлен на время собственного репорта позиции (<see cref="MoveCursorToFocus"/>): такой сдвиг курсора —
        /// перенос в точку фокуса, а не движение пользователя, и <see cref="OnPointerMoved"/> его пропускает.
        /// Событие модели синхронное и приходит внутри репорта — флага на время вызова достаточно.
        /// </summary>
        private static bool _selfReporting;

        /// <summary>
        /// Режим навигации: выставляется переходом фокуса, снимается только сдвигом курсора пользователем
        /// или опустевшим стеком групп. Переживает перенос фокуса в RememberedFocus и отключение фокусной цели —
        /// на нём держится авто-передача фокуса при push/pop/Ignore.
        /// </summary>
        private static bool _navMode;

        /// <summary>
        /// Запрошена авто-передача фокуса новой активной группе (push/pop/Ignore). Выполняется в ближайшем тике
        /// петли сопровождения (<see cref="FollowFocus"/>), а не синхронно в OnEnable/OnDisable группы: к тику
        /// дочерние цели успевают зарегистрироваться, стек — устояться. Явный переход фокуса
        /// (<see cref="SetFocusInternal"/>) запрос снимает.
        /// </summary>
        private static bool _pendingAutoFocus;

        /// <summary>
        /// Захват владения моделью фокуса, подписка на позицию курсора и запуск петли сопровождения.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            _model = VirtualCursorBus.Focus;
            _model.SetOwner(Key);

            VirtualCursorBus.Data.OnPointerMoved += OnPointerMoved;

            // Follow-петля: самоперепланирующаяся через TimeController.Accumulate (как у драйверов
            // ввода). На каждом кадре сверяет ScreenPoint текущего target'а с позицией курсора и, если тот
            // физически сместился (ScrollRect, layout group, анимация, DOTween на RectTransform), —
            // переносит курсор следом (ReportPointer + warp). Это важно, когда FocusCenterScrollRect
            // прокручивает контент под фокусным элементом: без follow'а курсор остался бы в точке
            // прежнего warp'а, визуально оторвавшись от элемента.
            TimeController.Accumulate(FollowFocus, FollowOwner);
        }

        /// <summary>Отклонить смену фокуса из обработчика фокуса (см. <see cref="_busy"/>).</summary>
        /// <returns><c>true</c> (с LogError) — вызов из обработчика фокуса, вызывающий должен выйти.</returns>
        private static bool Reject()
        {
            if (!_busy) return false;
            Debug.LogError("[VirtualCursorFocus] Смена фокуса из обработчика фокуса " +
                           "(onFocused/onUnfocused) — игнорирована. Эти события не должны менять фокус.");
            return true;
        }

        /// <summary>
        /// Отложить изменение стека групп или регистрации целей, пришедшее из обработчика фокуса (см. <see cref="_busy"/>).
        /// </summary>
        /// <param name="retry">Повтор вызова; выполнится после завершения внешней операции.</param>
        /// <returns><c>true</c> — вызов отложен, вызывающий должен выйти.</returns>
        private static bool Defer(System.Action retry)
        {
            if (!_busy) return false;
            Deferred.Enqueue(retry);
            return true;
        }

        /// <summary>Начало операции контроллера (вход в публичную/внутреннюю точку, способную уведомлять цели).</summary>
        private static void BeginOperation() => _operationDepth++;

        /// <summary>
        /// Конец операции. На выходе из внешней операции выполняет отложенные изменения по порядку; каждое — своя
        /// операция, его собственные отложенные изменения выполняются следом. Исключение одного не останавливает остальные.
        /// </summary>
        private static void EndOperation()
        {
            if (--_operationDepth > 0) return;

            while (Deferred.Count > 0)
            {
                var retry = Deferred.Dequeue();
                try
                {
                    retry();
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Уведомить target о смене фокуса под <see cref="_busy"/> — окно, в котором смена фокуса отклоняется,
        /// а изменения стека и регистрации откладываются.
        /// </summary>
        /// <param name="target">Уведомляемый target.</param>
        /// <param name="focused"><c>true</c> — фокус пришёл, <c>false</c> — ушёл.</param>
        private static void Notify(IFocusTarget target, bool focused)
        {
            _busy = true;
            try
            {
                if (focused) target.NotifyFocused();
                else target.NotifyUnfocused();
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Push группы в стек с учётом приоритета. Зовётся <see cref="FocusGroup"/>.OnEnable.
        /// Если активная группа сменилась — фокус старой уходит в её RememberedFocus (с NotifyUnfocused),
        /// затем запрашивается отложенная авто-передача фокуса новой активной группе (<see cref="_pendingAutoFocus"/>).
        /// Если активная группа не сменилась — только обновление стека.
        /// Из обработчика фокуса — откладывается; на выполнении пропускается, если группа к тому времени выключена.
        /// </summary>
        public static void PushGroup(FocusGroup group)
        {
            if (group == null) return;
            if (Defer(() =>
                {
                    if (group != null && group.isActiveAndEnabled) PushGroup(group);
                })) return;

            BeginOperation();
            try
            {
                // Гард фликера: двигаем фокус только если push реально сменил активную группу.
                // Push группы с меньшим приоритетом / Ignored оставляет ActiveGroup прежней — тогда
                // Transfer+auto-focus сняли бы и тут же вернули фокус на ТОТ ЖЕ target (лишние
                // NotifyUnfocused/NotifyFocused + повторный Warp). Симметрично гарду OnGroupIgnoreChanged.
                var before = _model.ActiveGroup;
                _model.PushGroup(group);
                if (ReferenceEquals(_model.ActiveGroup, before)) return;

                // Активная группа сменилась: фокус старой (если был) уходит в её RememberedFocus +
                // NotifyUnfocused (старая группа ещё в стеке — FindOwningGroup её найдёт), новая
                // активная получает фокус на ближайшем тике (её цели ещё не включились).
                TransferCurrentToRemembered();
                _pendingAutoFocus = true;
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Снять группу из стека. Зовётся <see cref="FocusGroup"/>.OnDisable или OnDestroy (идемпотентно;
        /// уничтоженная группа снимается по ссылке).
        /// Если это была активная группа — её фокус уходит в её RememberedFocus (с NotifyUnfocused),
        /// затем запрашивается отложенная авто-передача фокуса новой активной группе (<see cref="_pendingAutoFocus"/>).
        /// Из обработчика фокуса — откладывается; на выполнении пропускается, если группа к тому времени снова включена.
        /// </summary>
        public static void RemoveGroup(FocusGroup group)
        {
            if (ReferenceEquals(group, null)) return;
            if (Defer(() =>
                {
                    if (group != null && group.isActiveAndEnabled) return;
                    RemoveGroup(group);
                })) return;

            BeginOperation();
            try
            {
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
                    _pendingAutoFocus = true;
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Реакция на уход target'а из системы (его <see cref="FocusGroup.Unregister"/> — обычно
        /// <c>FocusTargetComponent.OnDisable</c>). Если уходящий target был текущим фокусом, снимаем
        /// висящий <c>CurrentFocus</c> (он теперь указывает на выбывший элемент) + <c>NotifyUnfocused</c>.
        /// В RememberedFocus target не пишется и курсор не трогается. Без этого модель до следующего
        /// <see cref="Navigate"/> держала бы ссылку на отключённый target.
        /// Из обработчика фокуса — откладывается; на выполнении пропускается, если target к тому времени снова активен.
        /// </summary>
        internal static void OnTargetUnregistered(IFocusTarget target)
        {
            if (target == null) return;
            if (Defer(() =>
                {
                    // Уничтоженный target не спрашиваем (IsActive бросил бы MissingReferenceException) — снимаем.
                    if (!(target is Object o && o == null) && target.IsActive) return;
                    OnTargetUnregistered(target);
                })) return;

            BeginOperation();
            try
            {
                if (!ReferenceEquals(_model.CurrentFocus.Value, target)) return;
                _model.SetCurrent(null, Key);
                Notify(target, false);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Реакция на runtime-смену <see cref="FocusGroup.Ignore"/>. Зовётся из setter'а
        /// свойства самой группы. Если смена Ignore повлияла на <see cref="FocusModel.ActiveGroup"/>
        /// (её либо скрыло, либо наоборот — вернуло более высокоприоритетную в игру),
        /// переносит фокус: NotifyUnfocused на старом target'е, запись в RememberedFocus владеющей группы,
        /// затем отложенная авто-передача фокуса новой активной группе (<see cref="_pendingAutoFocus"/>).
        ///
        /// Если смена Ignore на ActiveGroup не влияет (например, Ignore'нули группу
        /// в глубине стека, которая и так не была активной) — ничего не делаем, фликера
        /// фокуса нет.
        ///
        /// Фокуса нет (режим навигации мог остаться после отключения фокусной цели) — запрашивается
        /// авто-передача фокуса, как при push/pop: на тике она сработает только в режиме навигации.
        /// Из обработчика фокуса — откладывается и на выполнении оценивает состояние заново.
        /// </summary>
        internal static void OnGroupIgnoreChanged(FocusGroup group)
        {
            if (group == null) return;
            if (Defer(() => OnGroupIgnoreChanged(group))) return;

            BeginOperation();
            try
            {
                var current = _model.CurrentFocus.Value;
                if (current == null)
                {
                    _pendingAutoFocus = true;
                    return;
                }

                // Если текущий фокус всё ещё принадлежит активной группе — ничего не менялось
                // для него (Ignore затронул чужой слой стека). Пропускаем, чтобы не было фликера.
                var activeGroup = _model.ActiveGroup;
                if (activeGroup != null && ContainsTarget(activeGroup, current)) return;

                // CurrentFocus теперь вне активной группы: либо его группа только что стала
                // Ignored, либо выше в стеке unIgnored'нулась другая, которая его перекрыла.
                // Переносим: Remembered в владеющую группу (сохраняется для будущего возврата)
                // + NotifyUnfocused (визуал снимается немедленно). Затем auto-focus в новой
                // активной на ближайшем тике — только в nav mode (_navMode); иначе остаёмся
                // без фокуса, пока пользователь не нажмёт направление.
                TransferCurrentToRemembered();
                _pendingAutoFocus = true;
            }
            finally
            {
                EndOperation();
            }
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
        /// <param name="direction">Направление перехода (нормализуется; нулевой вектор игнорируется).</param>
        /// <param name="coneAngleDeg">Полуугол конуса выбора, градусы.</param>
        /// <param name="hideCursor">Скрыть курсор после перехода фокуса.</param>
        /// <param name="captureNearestIfFree">Без фокуса и без кандидатов в конусе — взять ближайший target группы.</param>
        public static void Navigate(Vector2 direction, float coneAngleDeg,
            bool hideCursor, bool captureNearestIfFree)
        {
            if (Reject()) return;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (VirtualCursorBus.Data == null) return;

            var activeGroup = _model.ActiveGroup;
            if (activeGroup == null) return;

            var current = _model.CurrentFocus.Value;
            var origin = current?.ScreenPoint ?? VirtualCursorBus.Data.ScreenPosition.Value;
            var dir = direction.normalized;
            var cone = Mathf.Abs(coneAngleDeg);

            IFocusTarget best = null;
            var bestDistSqr = float.MaxValue;

            var targets = activeGroup.Targets;
            foreach (var t in targets)
            {
                if (t == null || !t.IsActive) continue;
                // Явное исключение текущего — даже при origin == current.ScreenPoint
                // defensive-ный ReferenceEquals ловит случаи, где distSqr мог бы оказаться
                // ненулевым из-за реализации ScreenPoint (например, пересчёт между двумя
                // чтениями подряд даёт разные значения на долях пикселя).
                if (ReferenceEquals(t, current)) continue;

                var vec = t.ScreenPoint - origin;
                var distSqr = vec.sqrMagnitude;
                if (distSqr < 0.1f) continue;

                var angle = Vector2.SignedAngle(dir, vec);
                if (angle < -cone || angle >= cone) continue;

                if (!(distSqr < bestDistSqr)) continue;
                bestDistSqr = distSqr;
                best = t;
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

            BeginOperation();
            try
            {
                SetFocusInternal(best, hideCursor);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Выход из режима навигации: если есть <c>CurrentFocus</c> — сохраняет его в <c>RememberedFocus</c>
        /// владеющей группы, снимает (<c>SetCurrent(null)</c>) и зовёт <c>NotifyUnfocused</c>.
        /// Видимость курсора здесь не меняется — её задаёт источник, сдвинувший курсор.
        /// </summary>
        private static void ClearFocus()
        {
            if (Reject()) return;
            var current = _model.CurrentFocus.Value;
            if (current != null)
            {
                var owning = FindOwningGroup(current);
                if (owning != null) owning.RememberedFocus = current;

                _model.SetCurrent(null, Key);
                Notify(current, false);
            }
        }

        /// <summary>
        /// Снять текущий фокус (если был) и запомнить его в его ВЛАДЕЮЩЕЙ группе как
        /// RememberedFocus (не обязательно ActiveGroup — current может быть stale). <see cref="_lastHideCursor"/>
        /// не сбрасывается — следующая авто-передача фокуса продолжит в том же режиме видимости.
        /// </summary>
        private static void TransferCurrentToRemembered()
        {
            var current = _model.CurrentFocus.Value;
            if (current == null) return;

            var owning = FindOwningGroup(current);
            if (owning != null) owning.RememberedFocus = current;

            _model.SetCurrent(null, Key);
            Notify(current, false);
        }

        /// <summary>
        /// Авто-передача фокуса в новой активной группе — исполнение отложенного запроса (<see cref="_pendingAutoFocus"/>)
        /// из петли сопровождения. Выполняется только в режиме навигации
        /// (<see cref="_navMode"/>). Target берётся из RememberedFocus группы (если он жив и активен), иначе —
        /// ближайший к курсору по евклидовой дистанции. Пустой стек групп — фокус снимается, режим навигации завершается.
        /// </summary>
        private static void TryAutoFocusNewActiveGroup()
        {
            if (!_navMode) return; // Пользователь не в nav mode — пропускаем

            var activeGroup = _model.ActiveGroup;
            if (activeGroup == null)
            {
                // Стек пуст (всё закрылось) — выйти из nav mode аккуратно.
                _navMode = false;
                _model.SetCurrent(null, Key);
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

            SetFocusInternal(target, _lastHideCursor);
        }

        /// <summary>
        /// Перевести фокус на target: старый уведомляется и запоминается в своей группе, новый уведомляется
        /// и запоминается в своей; курсор переносится в точку target'а (репорт с флагом скрытия + варп ОС-мыши).
        /// Точка берётся после смены фокуса и уведомлений: подписчики фокуса (например, <see cref="FocusCenterScrollRect"/>)
        /// могут сдвинуть target синхронно. Тот же target — ничего не делает.
        /// </summary>
        private static void SetFocusInternal(IFocusTarget target, bool hideCursor)
        {
            var old = _model.CurrentFocus.Value;
            if (ReferenceEquals(old, target)) return;

            _lastHideCursor = hideCursor;
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

            _navMode = true;
            _pendingAutoFocus = false; // явный переход фокуса перекрывает отложенную авто-передачу
            MoveCursorToFocus(target.ScreenPoint, hideCursor);
        }

        /// <summary>
        /// Перенести курсор в точку фокуса: репорт позиции с флагом скрытия + варп ОС-мыши.
        /// Репорт идёт под <see cref="_selfReporting"/> — <see cref="OnPointerMoved"/> не примет его за
        /// движение пользователя и не снимет фокус. Эхо варпа (событие ОС-мыши в ту же точку) значимого сдвига не даёт
        /// по фильтру дрейфа на входе репорта (VirtualCursorController.ReportPointer).
        /// </summary>
        private static void MoveCursorToFocus(Vector2 pos, bool hideCursor)
        {
            _selfReporting = true;
            try
            {
                VirtualCursorController.ReportPointer(pos, hideCursor);
            }
            finally
            {
                _selfReporting = false;
            }

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
            foreach (var g in groups)
                if (g != null && ContainsTarget(g, target))
                    return g;

            return null;
        }

        /// <summary>Содержит ли группа этот target в своём списке <c>Targets</c>.</summary>
        private static bool ContainsTarget(FocusGroup group, IFocusTarget target)
        {
            if (group == null || target == null) return false;
            var ts = group.Targets;
            foreach (var t in ts)
                if (ReferenceEquals(t, target))
                    return true;

            return false;
        }

        /// <summary>
        /// Follow-петля: исполнение отложенной авто-передачи фокуса (<see cref="_pendingAutoFocus"/>) и
        /// сопровождение курсора за физически смещающимся target'ом. Запускается
        /// каждый кадр через <see cref="TimeController.Accumulate"/> (самоперепланирующаяся
        /// в finally — любой бросок в теле не рвёт цикл).
        /// Семантика (<see cref="TickFollow"/>):
        /// <list type="bullet">
        /// <item>Нет текущего фокуса / target неактивен → ничего не делаем.</item>
        /// <item>Точка target'а ближе <see cref="FollowToleranceSqr"/> к позиции курсора → ничего не делаем.</item>
        /// <item>Иначе переносим курсор: <c>ReportPointer</c> с <see cref="_lastHideCursor"/> + варп ОС-мыши.</item>
        /// </list>
        /// Это закрывает кейс «ScrollRect прокрутил контент / layout group пересчитался /
        /// анимация передвинула target» — target движется БЕЗ собственных событий, но
        /// <c>IFocusTarget.ScreenPoint</c> у <see cref="FocusTargetComponent"/> пересчитывается
        /// при каждом запросе из live-RectTransform, так что polling даёт актуальную точку.
        /// </summary>
        private static void FollowFocus()
        {
            try
            {
                if (_pendingAutoFocus)
                {
                    _pendingAutoFocus = false;
                    BeginOperation();
                    try
                    {
                        TryAutoFocusNewActiveGroup();
                    }
                    finally
                    {
                        EndOperation();
                    }
                }

                TickFollow();
            }
            finally
            {
                TimeController.Accumulate(FollowFocus, FollowOwner);
            }
        }

        /// <summary>Один шаг сопровождения: перенести курсор в точку текущего target'а, если она сместилась.</summary>
        private static void TickFollow()
        {
            var current = _model.CurrentFocus?.Value;
            if (current == null || !current.IsActive) return;
            var newPos = current.ScreenPoint;
            if ((newPos - VirtualCursorBus.Data.ScreenPosition.Value).sqrMagnitude < FollowToleranceSqr)
                return;

            MoveCursorToFocus(newPos, _lastHideCursor);
        }

        /// <summary>
        /// Реакция на значимый сдвиг курсора (<c>PointerModel.OnPointerMoved</c>) в режиме навигации: выход из режима
        /// и снятие фокуса (если он есть: фокус мог уйти раньше вместе с отключённой целью, а режим — остаться).
        /// Дрожание и эхо варпа событие не поднимают — это решает фильтр дрейфа на входе репорта (VirtualCursorController.ReportPointer). Собственный перенос курсора в
        /// точку фокуса (<see cref="MoveCursorToFocus"/>) пропускается.
        /// </summary>
        private static void OnPointerMoved()
        {
            if (_selfReporting) return;
            if (!_navMode) return;

            // Пользователь двинул курсор — выход из nav mode: снятие фокуса.
            _navMode = false;
            BeginOperation();
            try
            {
                ClearFocus();
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Активировать фокусный элемент — эмуляция клика левой кнопкой в точке цели по правилам EventSystem,
        /// как если бы рейкаст попал в <see cref="IFocusTarget.Target"/>:
        /// <list type="number">
        /// <item><c>PointerDown</c> — с подъёмом по иерархии до ближайшего <see cref="IPointerDownHandler"/>
        /// (получатель нажатия; без него — ближайший <see cref="IPointerClickHandler"/>);</item>
        /// <item><c>PointerUp</c> — получателю нажатия;</item>
        /// <item><c>PointerClick</c> — ближайшему по иерархии <see cref="IPointerClickHandler"/>.</item>
        /// </list>
        /// Полная последовательность нужна обработчикам, которые фиксируют клик не только по PointerClick
        /// (нажатие, отпускание, время между ними), а подъём — компонентам цели, висящим ниже обработчика
        /// (например, на зоне нажатия кнопки). Без фокуса или с уничтоженной целью — ничего не делает.
        /// </summary>
        public static void Submit()
        {
            if (!_model.HasFocus())
                return;

            var current = _model.CurrentFocus.Value;
            var go = current.Target;
            if (go == null)
                return;

            var pos = current.ScreenPoint;
            var ped = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = pos,
                pressPosition = pos,
                eligibleForClick = true,
                clickCount = 1,
                clickTime = Time.unscaledTime,
                rawPointerPress = go,
                pointerCurrentRaycast = new RaycastResult { gameObject = go, screenPosition = pos },
            };
            ped.pointerPressRaycast = ped.pointerCurrentRaycast;

            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            var pressHandler = ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
            if (pressHandler == null)
                pressHandler = clickHandler;

            ped.pointerPress = pressHandler;
            ped.pointerClick = clickHandler;

            if (pressHandler != null)
                ExecuteEvents.Execute(pressHandler, ped, ExecuteEvents.pointerUpHandler);

            if (clickHandler != null)
                ExecuteEvents.Execute(clickHandler, ped, ExecuteEvents.pointerClickHandler);
        }
    }
}