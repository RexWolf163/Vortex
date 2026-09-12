using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Клапан перехвата (ТЗ 2.4). Слушает события ввода, пока клапан открыт или пока не отпущены проглоченные
    /// нажатия; в остальное время подписок нет.
    ///
    /// Ловятся только устройства группы слота. Пойманное нажатие гасится вместе со своим событием
    /// (<c>handled</c>): событие не записывается в состояние устройства и до игровых команд не доходит. Поэтому
    /// удержание проглоченных клавиш клапан отслеживает сам и гасит события устройства, пока они не отпущены, —
    /// иначе следующее событие того же устройства донесло бы зажатую клавишу до игры. Побочный эффект: пока
    /// проглоченная клавиша зажата, прочие нажатия этого устройства тоже гасятся.
    ///
    /// Путь кандидата обобщается до самого общего layout'а, который ещё входит в группу слота и содержит контрол:
    /// кнопка DualSense записывается как <c>&lt;Gamepad&gt;/buttonSouth</c>, если группа — Gamepad.
    ///
    /// Решение по кандидату откладывается до конца апдейта ввода: операция меняет биндинги, а менять их посреди
    /// обработки событий Input System небезопасно.
    /// </summary>
    public sealed partial class RebindController
    {
        public CaptureValve Capture { get; } = new();

        private DeviceGroupSettings _captureGroup;

        /// <summary>Постоянные фильтры кандидатов от вьюшек: кандидат принят, если его пропустили все.</summary>
        private readonly List<Func<BindingValue, bool>> _captureFilters = new();

        /// <summary>Проглоченные и ещё не отпущенные нажатия.</summary>
        private readonly HashSet<InputControl> _swallowed = new();

        /// <summary>Удерживаемые модификаторы текущего перехвата в порядке нажатия (пути — в <see cref="CaptureValve.Held"/>).</summary>
        private readonly List<InputControl> _heldModifiers = new();

        /// <summary>Аккорд модификаторов отработан (был триггер или отказ): их отпускание клавишей не становится.</summary>
        private bool _chordSpent;

        private bool _listening;
        private BindingValue _pendingCandidate;
        private RejectReason _pendingReject;
        private bool _pendingNotify;

        // Буферы без аллокаций на событие.
        private readonly List<InputControl> _released = new();
        private readonly List<InputControl> _candidates = new();

        // ── Открытие и закрытие ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Открыть клапан для слота. Открытый клапан перенацеливается, прежний перехват завершается
        /// <c>Cancelled</c>. Ответ — причина, по которой клапан не открылся; <see cref="RejectReason.None"/> — открыт.
        /// </summary>
        public RejectReason SaveSignalForBind(string bindKey)
        {
            var reason = ResolveSlot(bindKey, out _, out var slot);
            if (reason != RejectReason.None)
                return reason;

            if (Capture.IsOpen)
                CloseCapture(RebindResult.Cancelled());

            _captureGroup = Model.GroupsByKey[slot.Group];
            Capture.BindKey = slot.BindKey;
            Capture.LastResult = null;
            // Идемпотентно: подписчик Cancelled мог уже открыть клапан заново изнутри CloseCapture.
            Application.focusChanged -= OnCaptureFocusChanged;
            Application.focusChanged += OnCaptureFocusChanged;
            StartListening();
            RebindBus.RaiseCaptureChanged(Capture);
            return RejectReason.None;
        }

        /// <summary>Прервать перехват — <c>Cancelled</c>, слот остаётся как был.</summary>
        public void CancelSaving()
        {
            if (Capture.IsOpen)
                CloseCapture(RebindResult.Cancelled());
        }

        /// <summary>
        /// Добавить постоянный фильтр кандидатов. Кандидат, для которого фильтр вернул <c>false</c>, игнорируется
        /// и проходит в игру и UI как обычное нажатие.
        /// </summary>
        public void AddCaptureFilter(Func<BindingValue, bool> filter)
        {
            if (filter != null && !_captureFilters.Contains(filter))
                _captureFilters.Add(filter);
        }

        public void RemoveCaptureFilter(Func<BindingValue, bool> filter) => _captureFilters.Remove(filter);

        private bool PassesFilters(BindingValue candidate)
        {
            foreach (var filter in _captureFilters)
                if (!filter(candidate))
                    return false;
            return true;
        }

        private void OnCaptureFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
                CancelSaving();
        }

        private void CloseCapture(RebindResult result)
        {
            Application.focusChanged -= OnCaptureFocusChanged;
            Capture.BindKey = null;
            Capture.LastResult = result;
            Capture.Held.Clear();
            _heldModifiers.Clear();
            _chordSpent = false;
            _captureGroup = null;
            _pendingCandidate = null;
            _pendingReject = RejectReason.None;
            _pendingNotify = false;
            StopListeningIfIdle();
            RebindBus.RaiseCaptureChanged(Capture);
        }

        private void StartListening()
        {
            if (_listening)
                return;
            _listening = true;
            InputSystem.onEvent += (Action<InputEventPtr, InputDevice>)OnCaptureEvent;
            InputSystem.onAfterUpdate += OnCaptureAfterUpdate;
        }

        private void StopListeningIfIdle()
        {
            if (!_listening || Capture.IsOpen || _swallowed.Count > 0)
                return;
            _listening = false;
            InputSystem.onEvent -= (Action<InputEventPtr, InputDevice>)OnCaptureEvent;
            InputSystem.onAfterUpdate -= OnCaptureAfterUpdate;
        }

        // ── События ввода ───────────────────────────────────────────────────────────────────────────

        private void OnCaptureEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            var suppress = TrackSwallowed(eventPtr, device);
            if (Capture.IsOpen && _pendingCandidate == null && GroupResolver.Matches(_captureGroup, device.layout))
                suppress |= ScanPresses(eventPtr, device);
            if (suppress)
                eventPtr.handled = true;
        }

        /// <summary>Отпускания проглоченных нажатий. <c>true</c> — какое-то из них на этом устройстве ещё зажато.</summary>
        private bool TrackSwallowed(InputEventPtr eventPtr, InputDevice device)
        {
            if (_swallowed.Count == 0)
                return false;

            var held = false;
            _released.Clear();
            foreach (var control in _swallowed)
            {
                if (control.device != device || !TryReadPressed(control, eventPtr, out var pressed))
                    continue;
                if (pressed)
                    held = true;
                else
                    _released.Add(control);
            }

            foreach (var control in _released)
            {
                _swallowed.Remove(control);
                OnModifierReleased(control);
            }

            return held;
        }

        /// <summary>Новые нажатия устройства группы. <c>true</c> — нажатие поймано, событие гасится.</summary>
        private bool ScanPresses(InputEventPtr eventPtr, InputDevice device)
        {
            _candidates.Clear();
            var hasPhysical = false;
            // Синтетические включены явно: без них перебор пропускает колесо мыши (scroll/up, scroll/down) и
            // направления стиков. Дубли физических клавиш (ctrl при leftCtrl, anyKey) отсекаются ниже.
            const InputControlExtensions.Enumerate flags = InputControlExtensions.Enumerate.IgnoreControlsInCurrentState
                                                           | InputControlExtensions.Enumerate.IncludeSyntheticControls;
            foreach (var control in eventPtr.EnumerateControls(flags, device))
            {
                if (control.noisy || _swallowed.Contains(control) || !IsCandidateControl(control)
                    || IsPointerMotion(control, device))
                    continue;
                if (!TryReadPressed(control, eventPtr, out var pressed) || !pressed)
                    continue;
                // Зажато с момента открытия клапана — не ловится, пока не отпущено.
                if (IsPressedNow(control))
                    continue;
                _candidates.Add(control);
                hasPhysical |= !control.synthetic;
            }

            var suppress = false;
            foreach (var control in _candidates)
            {
                if (_pendingCandidate != null)
                    break;
                // Синтетический дубль физической клавиши того же события (ctrl при leftCtrl, anyKey).
                if (hasPhysical && control.synthetic)
                    continue;

                var path = GeneralPath(control);
                if (ServiceRule.IsKeyboardModifier(path))
                {
                    _heldModifiers.Add(control);
                    Capture.Held.Add(ModifierPath(path));
                    _pendingNotify = true;
                    if (_heldModifiers.Count > 2 && !_chordSpent)
                    {
                        _chordSpent = true;
                        _pendingReject = RejectReason.TooManyModifiers;
                    }
                }
                else
                {
                    var candidate = new BindingValue(path, Capture.Held.ToArray());
                    if (!PassesFilters(candidate))
                        continue;
                    _chordSpent = true;
                    // Запрещённую клавишу назначить нельзя — ловить её незачем: отказ для вьюшки, нажатие уходит
                    // в игру. Так работает отмена перехвата клавишей из запрещённых (Esc → UICancel).
                    if (ForbiddenMatcher.IsForbidden(candidate, Settings?.Forbidden))
                    {
                        _pendingReject = RejectReason.ForbiddenKey;
                        continue;
                    }

                    _pendingCandidate = candidate;
                }

                _swallowed.Add(control);
                suppress = true;
            }

            return suppress;
        }

        /// <summary>
        /// Отпущен удерживаемый модификатор. Одиночный без триггера — кандидат «по отпусканию»; два без триггера —
        /// отказ. Дальнейшие отпускания того же аккорда кандидатами не становятся.
        /// </summary>
        private void OnModifierReleased(InputControl control)
        {
            var index = _heldModifiers.IndexOf(control);
            if (index < 0)
                return;

            var path = Capture.Held[index];
            var single = _heldModifiers.Count == 1;
            _heldModifiers.RemoveAt(index);
            Capture.Held.RemoveAt(index);
            _pendingNotify = true;

            if (!_chordSpent)
            {
                _chordSpent = true;
                if (!single)
                {
                    _pendingReject = RejectReason.InvalidTrigger;
                }
                else if (_pendingCandidate == null)
                {
                    var candidate = new BindingValue(path);
                    if (PassesFilters(candidate))
                        _pendingCandidate = candidate;
                }
            }

            if (_heldModifiers.Count == 0)
                _chordSpent = false;
        }

        // ── Решение ─────────────────────────────────────────────────────────────────────────────────

        private void OnCaptureAfterUpdate()
        {
            if (Capture.IsOpen)
            {
                var candidate = _pendingCandidate;
                var reject = _pendingReject;
                var notify = _pendingNotify;
                _pendingCandidate = null;
                _pendingReject = RejectReason.None;
                _pendingNotify = false;

                if (candidate != null)
                {
                    Decide(candidate);
                }
                else if (reject != RejectReason.None)
                {
                    Capture.LastResult = RebindResult.Rejected(reject);
                    RebindBus.RaiseCaptureChanged(Capture);
                }
                else if (notify)
                {
                    RebindBus.RaiseCaptureChanged(Capture);
                }
            }

            // Отпускание контрола отключённого устройства не придёт — иначе прослушка висела бы до переподключения.
            _swallowed.RemoveWhere(control => !control.device.added);
            StopListeningIfIdle();
        }

        /// <summary>
        /// Назначение по 2.3. Отказ по свойствам самой клавиши оставляет клапан открытым; внутрикартовый конфликт
        /// и успех закрывают его.
        /// </summary>
        private void Decide(BindingValue candidate)
        {
            var result = Place(Capture.BindKey, candidate, take: false);
            if (result.Status == RebindStatus.Rejected && KeepsOpen(result.Reason))
            {
                Capture.LastResult = result;
                RebindBus.RaiseCaptureChanged(Capture);
                return;
            }

            CloseCapture(result);
        }

        private static bool KeepsOpen(RejectReason reason) =>
            reason is RejectReason.ForbiddenKey or RejectReason.InvalidTrigger or RejectReason.TooManyModifiers
                or RejectReason.AmbiguousModifiers or RejectReason.DeviceNotInGroup;

        // ── Контролы и пути ─────────────────────────────────────────────────────────────────────────

        /// <summary>Кнопка или синтетическое направление оси (колесо мыши, направления стика).</summary>
        private static bool IsCandidateControl(InputControl control) =>
            control is ButtonControl || control is AxisControl && ServiceRule.IsHalfAxis(control.name);

        /// <summary>
        /// Направление движения указателя (<c>delta/up</c> и т.п.): полуось, но не кнопка. Контрол не помечен
        /// noisy — без этой проверки любое движение мыши стало бы кандидатом.
        /// </summary>
        private static bool IsPointerMotion(InputControl control, InputDevice device) =>
            device is Pointer pointer && control.parent == pointer.delta;

        private static bool TryReadPressed(InputControl control, InputEventPtr eventPtr, out bool pressed)
        {
            pressed = false;
            if (control is not AxisControl axis || !axis.ReadValueFromEvent(eventPtr, out float value))
                return false;
            pressed = value >= PressPoint(axis);
            return true;
        }

        private static bool IsPressedNow(InputControl control) =>
            control is AxisControl axis && axis.ReadValue() >= PressPoint(axis);

        private static float PressPoint(AxisControl axis) =>
            axis is ButtonControl button ? button.pressPointOrDefault : InputSystem.settings.defaultButtonPressPoint;

        /// <summary>
        /// Путь контрола от самого общего layout'а устройства, который ещё входит в группу слота и содержит
        /// контрол, — как в штатном переназначении Unity.
        /// </summary>
        private string GeneralPath(InputControl control)
        {
            var device = control.device;
            var relative = control.path.Substring(device.path.Length);
            var best = device.layout;
            for (var layout = InputSystem.GetNameOfBaseLayout(best);
                 !string.IsNullOrEmpty(layout) && GroupResolver.Matches(_captureGroup, layout);
                 layout = InputSystem.GetNameOfBaseLayout(layout))
            {
                if (HasControl(layout, relative))
                    best = layout;
            }

            return $"<{best}>{relative}";
        }

        private static bool HasControl(string layout, string relative)
        {
            try
            {
                return InputControlPath.TryGetControlLayout($"<{layout}>{relative}") != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Сторона модификатора по настройке: без различения сторон — общий shift/ctrl/alt.</summary>
        private string ModifierPath(string path)
        {
            if (DistinguishSides)
                return path;

            var slash = path.LastIndexOf('/');
            var key = path.Substring(slash + 1);
            var family = key.EndsWith("shift", StringComparison.OrdinalIgnoreCase) ? "shift"
                : key.EndsWith("ctrl", StringComparison.OrdinalIgnoreCase) ? "ctrl"
                : key.EndsWith("alt", StringComparison.OrdinalIgnoreCase) ? "alt"
                : key;
            return path.Substring(0, slash + 1) + family;
        }
    }
}
