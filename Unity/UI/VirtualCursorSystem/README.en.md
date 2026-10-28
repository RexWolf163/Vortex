# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Assembly:** `ru.vortex.unity.virtualcursorsystem`

---

## Purpose

A multi-source virtual cursor for UGUI projects. A single screen position (`ScreenPosition`) is fed by any source — mouse, gamepad, keys, touch — and is the single source of truth for cursor rendering, UGUI input, focus navigation and screen→world projection.

Sources are implemented as **drivers** (`InputDriver`), listed in the `InputDriverSet` settings asset and connected at startup by the `CursorInputLoader`. The cursor is a **cross-system entity**: there is no situational input gate, a connected driver is always active.

The look is a **string skin**: the controller resolves the final skin name from the hide flag (from the last input source) and a LIFO stack of skin requests (e.g. from hover zones). A renderer shows the skin by name: `UiCursorRenderer` (UGUI via `UIStateSwitcher`) or `OsCursorRenderer` (system OS cursor).

**Focus navigation** (`Focus/`): a `FocusGroup` collects child `FocusTargetComponent`s into a navigation context; groups live in a priority stack. `UINavigationDriver` picks the nearest target in a cone for a direction, moves the cursor (and the OS mouse) there and optionally hides it; submit activates the focused element.

**The whole package is gated by an SDK Settings toggle** (`cursorInputSdk` → define `USING_VORTEX_CURSOR`, `defineConstraints` in the asmdef). The exception is the partial file holding the toggle itself (`DefineSettings/SdkSettings.CursorInput.cs`), which joins the `SdkSettings` assembly via `.asmref` and is always visible.

**Versus `CursorSystem`:** `CursorSystem` — OS cursor + UGUI hover, mouse-only. `VirtualCursorSystem` — virtual position + source arbitration + pluggable drivers + focus navigation. With `USING_VORTEX_CURSOR` enabled the legacy `CursorSystem` menu is hidden.

Out of scope:
- Game triggers/click mechanics — consumer level; the package provides position, actions, skin, projection.
- Interpreting a world hit — the package returns a raw `RaycastHit`.
- Enabling Input Action maps — the project's responsibility (see "Input drivers").

---

## Dependencies

| Dependency | Purpose |
|-----------|---------|
| `Unity.InputSystem` | `InputAction`, `Mouse.WarpCursorPosition`, EnhancedTouch (direct touches of the UGUI module) |
| `UnityEngine.UI` / `EventSystems` | UGUI renderer, `IPointerXxx` in handlers, `ExecuteEvents` for submit, `StandaloneInputModule`/`BaseInput` for the UGUI module |
| `Vortex.Unity.InputBusSystem` | `InputController` — action resolution by "Map/Action" id, LIFO subscription |
| `Vortex.Core.LoaderSystem` | `IProcess`/`Loader` — connecting drivers in the loading pipeline |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — auto-provisioning `InputDriverSet` in `Resources/Settings` |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate` — per-frame loops (driver tick, focus follow) |
| `Vortex.Sdk.SdkSettingsSystem` | module toggle + `DefineSymbol("USING_VORTEX_CURSOR")` |
| `Vortex.Core.Extensions.ReactiveValues` / `Vortex.Unity.Extensions.ReactiveValues` | `ReactiveValue<T>`, `StringData`, `BoolData`, `Vector2Data` with owner protection |
| `ru.vortex.unity.ui.misc` | `UIStateSwitcher` (`UiCursorRenderer` skins), `TweenerHub` (`PointerActionTweenerHandler`) |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ValueSelector]` (action id dropdown), `[ToggleButton]` |
| Sirenix Odin Inspector | inspector attributes, `[SerializeReference]` driver list |

Drivers do **not** use `InputActionProperty`: a binding is a string action id resolved at runtime through `InputController`.

---

## Architecture

```
[VirtualCursorBus] (static facade)
 ├─ PointerModel Data      ← field owner: VirtualCursorController
 ├─ FocusModel   Focus     ← field owner: VirtualCursorFocusController
 ├─ GestureModel Gesture   ← field owner: VirtualCursorController
 └─ const HideCursor = "hidden"

[GestureModel] (IReactiveData, runtime, NOT saved)
 └─ GestureFrameData      Frame           ← impulse: Fingers · Phase · Center · Pan · Spread · Scale · Rotation

[PointerModel] (IReactiveData, runtime, NOT saved)
 ├─ Vector2Data           ScreenPosition  ← position truth
 ├─ PointerActionMaskData Actions         ← bitmask of simultaneous actions
 ├─ StringData            Skin            ← final skin ("hidden" / requested / "" = base)
 ├─ BoolData              IsOverUI
 ├─ Vector2Data           ScrollDelta     ← scroll impulse
 ├─ StringData            LastDevice      ← layout of the last input device ("" — not determined)
 └─ event OnPointerMoved                  ← significant move (drift filter)

[VirtualCursorController] (static, partial)
 ├─ ReportPointer(pos, hidesCursor, device) · ReportDevice(device) · SetAction(action, active, source) · ClearActions · SetOverUI · ReportScroll · ReportGesture   (internal — drivers)
 ├─ OnSkin(skin, owner) / OffSkin(owner)  — PointerModel extensions, LIFO stack of skin requests
 └─ Projection: RegisterCamera/UnregisterCamera (LIFO) · TryGetWorldHit · GetWorldProjection · InvalidateProjection

Input layer:
  [InputDriverSet] (SO, ICoreAsset)  → [SerializeReference] InputDriver[]   (Resources/Settings)
  [CursorInputLoader] (IProcess)     → Loader.Register · Resources.Load + failfast · connect per platform
                                        · tick via Accumulate (+anti-spam) · Disconnect on App.OnExit
  [InputDriver] (POCO, abstract): Connect/Disconnect · NeedsTick/Tick · HidesCursor · SupportsPlatform
     ├─ MouseInputDriver   (mouse position, no filter of its own)
     ├─ DirectInputDriver  (move vector, NeedsTick, speedCurve over hold time × stick², OS mouse warp)
     ├─ ActionInputDriver  (buttons → Actions bits)
     ├─ ScrollInputDriver  (4 directions → ScrollDelta: pulse + hold)
     ├─ TouchInputDriver   (HideOnly / AbsolutePosition / Delta + platform filter)
     ├─ MultiTouchInputDriver (finger → action: cursor position or mask action + platform filter)
     ├─ GestureInputDriver (gesture frame → Gesture.Frame; Tap/Hold with N fingers → mask action)
     └─ UINavigationDriver (4 directions → Navigate, submit → Submit)

Focus navigation (Focus/):
  [FocusModel]  → priority group stack · ActiveGroup (first non-Ignore from top) · CurrentFocus
  [FocusGroup]  (MonoBehaviour) → push/pop on OnEnable/OnDisable · Priority · Ignore · Targets · RememberedFocus
  [VirtualCursorFocusController] (static) → PushGroup/RemoveGroup · Navigate · Submit
                                            · leaves navigation mode on cursor movement · follows the target
  [IFocusTarget] (contract) → ScreenPoint · IsActive · NotifyFocused/NotifyUnfocused · Target

Scene components (Handlers/):
  CursorHoverZone · CameraProvider · FocusTargetComponent · FocusCenterScrollRect · FocusDropDownSelected
  PointerActionHandler · PointerActionTweenerHandler

Rendering (Render/): UiCursorRenderer (UGUI + UIStateSwitcher) | OsCursorRenderer (Cursor.SetCursor)

UGUI module (UiModule/):
  [VirtualCursorInputModule] (StandaloneInputModule on the EventSystem) → standard UGUI processing · direct touches per platform
  [VirtualCursorBaseInput]   (BaseInput) → the UGUI "mouse" = PointerModel · touches from EnhancedTouch
```

### Data flow

```
Source (mouse/stick/touch/keys)
   → InputDriver (action by id through InputController)
        → VirtualCursorController.ReportPointer / SetAction / ReportScroll / ReportGesture
             → PointerModel (ScreenPosition / Actions / Skin / ScrollDelta)
             │    ├→ renderer (position + skin)
             │    ├→ VirtualCursorFocusController (OnPointerMoved → leave navigation mode)
             │    ├→ PointerActionTweenerHandler (Actions → tweeners)
             │    ├→ Projection (on request) → RaycastHit
             │    └→ VirtualCursorBaseInput → VirtualCursorInputModule → UGUI events (hover/click/drag/scroll)
             └→ GestureModel (Frame) → gesture consumers (zoom, pan, rotation)
```

Initialization: `VirtualCursorController` and `VirtualCursorFocusController` start via `[RuntimeInitializeOnLoadMethod]`; the bus models are created statically and available from the first access.

---

## Key concepts

### Input drivers as a pluggable module
- `InputDriver` — an abstract **POCO**: `Connect()`/`Disconnect()`, `NeedsTick`/`Tick(dt)`, `HidesCursor`, `SupportsPlatform(platform)`. Actions are resolved by a "Map/Action" string id through `InputController` (`[ValueSelector]` dropdown in the inspector).
- **Drivers do not enable action maps.** Enabling a map is the project's job (e.g. an `InputMapHandler` in the scene); without an enabled map action events don't arrive and polling (`ReadValue`) returns zero.
- `InputDriverSet` — SO list of drivers (`[SerializeReference]`), `ICoreAsset` → auto-created at `Resources/Settings/InputDriverSet.asset`.
- `CursorInputLoader` — `IProcess`: registers in `Loader`, in `RunAsync` loads the set, connects the drivers for the current platform, starts the per-frame tick for `NeedsTick` drivers. **Failfast**: module enabled but the asset is missing, the list is empty, or no driver supports the current platform → exception. On `App.OnExit` all drivers are disconnected.
- An empty or unresolved action id — `LogError` "Ошибка настройки драйвера управления" in the `Mouse`/`Direct`/`Action`/`Touch`/`MultiTouch`/`Gesture`/`UINavigation` drivers; `ScrollInputDriver` silently disables the direction.

### Source arbitration and hiding
`ReportPointer(pos, hidesCursor)` sets the position (always) and visibility: a hiding source hides the cursor on any report, a non-hiding one brings it back only with a significant move (drift filter, see below) — jitter and warp echo do not clear hiding. A driver declares `HidesCursor` (`TouchInputDriver` in HideOnly/AbsolutePosition → `true`, in Delta → `false`; `MultiTouchInputDriver` and `GestureInputDriver` — the `hidesCursor` field); focus navigation passes its own `hideCursor` flag. A significant move from another source (mouse, stick) brings the cursor back.

### Cursor skin
The final `Skin` is resolved by the controller (`GetSkinName`):
1. Cursor hidden by the last source → `VirtualCursorBus.HideCursor` (`"hidden"`).
2. Otherwise — the top request of the LIFO skin stack.
3. Otherwise — empty string (the renderer's base skin).

Requests: `VirtualCursorBus.Data.OnSkin(skin, owner)` drops the owner's previous request and pushes the new one on top; `OffSkin(owner)` removes it. All three `Skin` writers (`ReportPointer`, `OnSkin`, `OffSkin`) go through the same rule — a hidden cursor stays hidden regardless of requests.

### Hover zones (`CursorHoverZone`)
Requests its skin on `PointerEnter`, removes it on `PointerExit`/`OnDisable`. **Placement rule: one zone — one own raycast.** UGUI sends `PointerEnter` to the raycast target and its ancestors, so a zone must not be an ancestor of another zone's raycast object (otherwise two zones share one raycast and the order of their requests is undefined). Nested zones are laid out as siblings, each with its own `Graphic` (`raycastTarget = true`).

### UGUI integration
UGUI takes input from the cursor model, not from devices: `VirtualCursorInputModule` (a `StandaloneInputModule` subclass) goes on the EventSystem instead of `InputSystemUIInputModule`; its input source `VirtualCursorBaseInput` provides:
- position — `ScreenPosition`;
- buttons 0/1/2 — `BaseClick`/`ContextClick`/`AltClick` of the `Actions` mask; press and release edges are captured from mask events and held until the end of the module frame — a press and release within one frame are not lost;
- scroll — the sum of `ScrollDelta` impulses over the frame;
- Selectable navigation axes and buttons — 0/false (the package's focus subsystem replaces navigation).

Raycast, Enter/Exit, Down/Up/Click, drag threshold and scroll are the standard `StandaloneInputModule` logic. So click, hover and drag work from any source the drivers feed into the model (mouse, gamepad, keyboard), without a physical mouse (consoles) and without an OS cursor warp. The legacy `UnityEngine.Input` is not called (except IME — same as the standard `BaseInput`); the module is compatible with "Input System only". The module has no actions to configure.

**Touches.** `nativeTouch` + `nativeTouchPlatforms` (`TouchPlatformFilter`, default `MobileOnly`): on a matching platform the module enables EnhancedTouch and feeds touches to UGUI directly — multitouch, each finger a separate pointer; in a frame with touches the cursor part is not processed. On other platforms touches do not reach UGUI — the touchscreen works through drivers (e.g. `TouchInputDriver` in Delta as a trackpad).

**Not supported:** Selectable navigation, pen (pressure/tilt), XR pointers.

The OS mouse warp (`DirectInputDriver.warpSystemMouse`, `TouchInputDriver` in AbsolutePosition, focus navigation) is no longer needed for UI — it keeps the OS cursor next to the virtual one so a physical mouse continues from its spot.

**Drift filter** — at the position report entry (`VirtualCursorController.ReportPointer`), shared by all sources; drivers keep no filters of their own. The position is always written; a move is significant if a single report moves more than 3 px (`DriftStepSqr` = 9 px²) or the shift from the drift center exceeds 5 px (`DriftRadiusSqr` = 25 px²); the center moves to the point of the significant move. A significant move clears hiding for a non-hiding source and raises `PointerModel.OnPointerMoved` (after position and skin, synchronously within the report). Fast movement is significant on every step, slow movement once the radius accumulates, jitter in place — never. The controllers' own cursor moves (focus transition, follow) fire the event too — the subscriber filters them out itself.

The shift accumulates only within one source — the input device of the report (moves without a device, e.g. focus, are a separate source). A report from a different source than the previous one is not significant and re-anchors the filter: the drift center moves to its point. Otherwise a source would get credit for another source's accumulation — the OS mouse warp echo would top up the threshold after a slow stick and be credited to the mouse. The cost: while warp echo interleaves the stick reports (every few frames), a slow stick (under 3 px per frame) does not accumulate the radius and does not raise `OnPointerMoved` — a confident deflection is what leaves navigation mode and hiding.

OS mouse warp echo arrives with a rounding error to the OS pixel (≈0.7 px; with a scaled window, e.g. a Game view smaller than the render resolution, up to the OS pixel size, ≈2 px per axis). Echo follows the report of the warping source, so it is not significant by the source change — it does not clear hiding, does not leave navigation mode and does not change `LastDevice`; the focus follow tolerance (2 px) is above the rounding error in an unscaled window — echo that nudged the cursor by a fraction of a pixel does not trigger another warp.

**Last input device** — `PointerModel.LastDevice`: the layout of the device of the last user input (`Mouse`, `Keyboard`, `DualShock4GamepadHID`, `Touchscreen`…), empty — not determined. Written by the controller (`ReportDevice`); drivers pass the device of their input (`InputDriver.DeviceOf(action)` — the device of the action's active control): an action press (`ActionInputDriver`, a `UINavigationDriver` step and submit, scroll, multitouch and gesture presses) — always; pointer movement — on a significant move and on a hiding source's report; `DirectInputDriver` — on every tick outside the dead zone, bypassing the drift filter. Cursor moves without a device (focus) do not change it. The same layout — no event. The field is shared: e.g. control glyphs by the last device are built on it (`InputActionView` of the RebindSystem package).

`PointerActionHandler` catches standard UGUI events from any source and splits them by button (Left/Right/Middle → `BaseClick`/`ContextClick`/`AltClick`). An event of another button is passed further up the hierarchy from the parent — a handler on a `Button` descendant does not swallow its click; its own button is consumed.

### Action mask
`PointerAction`: `None`, `BaseClick` (LMB), `ContextClick` (RMB), `AltClick` (MMB). **Do not reorder** — the index is the mask bit and the dominance priority. `PointerActionMask` is a `readonly struct` over `int`: simultaneous actions as bits, `Dominant()` — the active one with the lowest index. `None` has no bit and is never active. A bit is held by a set of holders: a source (an `ActionInputDriver` binding, a `MultiTouchInputDriver` finger) takes and releases the action on its own behalf; the bit stays active while at least one holder remains — releasing one of two actions or fingers on the same action does not clear it. Take and release are idempotent per source. `ActionInputDriver` takes on `performed` and releases on `canceled` (Input System sends `canceled` on window focus loss — no sticking).

### DirectInputDriver: speed
`speed × speedCurve(holdTime) × stick²`, direction — the normalized move vector. `holdTime` — seconds the stick is held outside the dead zone (the edge belongs to the zone, so with `deadzone = 0` a resting stick does not move the cursor): it resets to 0 instantly inside it and grows outside it up to the time of the curve's last key, no further. The curve defines the whole acceleration profile: X — seconds, Y — multiplier for `speed`; a constant curve means no acceleration. `stick` — deflection renormalized from the dead-zone edge (0 at the edge, 1 at full), squared — precision at small deflections. On `Connect` — `LogWarning` if the curve's last key ≈ 0; `deadzone` above 0.5 — `LogWarning`, 1 or more — `LogError` (the stick does not move the cursor). Ticks on `unscaledDeltaTime`, works during pause. The position is clamped to the screen. The stick's device goes to `LastDevice` on every tick outside the dead zone — deflection beyond it is intentional, the drift filter is not needed to pick the device.

### ScrollInputDriver: pulse + hold
Four direction Button actions. **Pulse** — a `pulseStep` impulse on every `performed` (mouse wheel). **Hold** — every frame while a direction is held, `holdSpeed × dt` (D-pad, keys, stick as Button). Either model is disabled by 0 in its field. Impulses are published to `PointerModel.ScrollDelta` (an impulse equal to the previous one is published forcibly — consecutive wheel clicks are not lost); `VirtualCursorInputModule` delivers them to UGUI.

### TouchInputDriver: modes and platform filter
- **HideOnly** (default) — a touch only hides the cursor; the current cursor position is reported (no movement — only the hide flag goes through), the action value is not read. The click itself is a direct touch of `VirtualCursorInputModule` (`nativeTouch` on the platform). For Android.
- **AbsolutePosition** — the cursor jumps to the touch point and hides; optional OS mouse warp.
- **Delta** — trackpad: the finger delta (× `deltaSpeedMultiplier`) moves the cursor, the cursor stays visible, clamped to the screen. No warp. For desktop touchscreens.

`TouchPlatformFilter`: `All` / `MobileOnly` (Android/iOS, not connected in the Editor) / `DesktopOnly` (Standalone + all Editors). A HideOnly + Delta pair is split by the filter — otherwise on mobile Delta (non-hiding) would override HideOnly.

### MultiTouchInputDriver: multitouch fingers
"Finger → action" bindings. The finger (`TouchFinger`) is the touch order, the Input System `Touchscreen.touches[i]` slot. The action is bound to touches as a whole (`<Touchscreen>/touch*/position`, `<Touchscreen>/touch*/press`); every frame the driver takes its control on the selected finger and acts by control type:
- **Vector2** (position) — the cursor follows the finger: reported while the touch is in progress and the point moved (immediately on touch start); hiding per `hidesCursor`.
- **Button** (press, tap) — the state sets or clears the binding's `PointerAction` in the `Actions` mask.

No position or button control on the finger — `LogError` once per binding. A disabled action (map disabled) — the touch is over for the driver: the action is released, cursor following resets. `Disconnect` clears only its own actions. Platforms — `TouchPlatformFilter`. With `VirtualCursorInputModule.nativeTouch` on the same platform the module's cursor part is not processed in touch frames — the driver's actions will not reach UGUI.

### GestureInputDriver: multitouch gestures
Touches come through an action bound to `<Touchscreen>/touch*/position` (controls of touches in progress, keyed by `touchId`). Two layers:

**Gesture frame** (`VirtualCursorBus.Gesture.Frame`, `GestureFrame`) — while two or more fingers are on screen:

| Field | Meaning |
|---|---|
| `Fingers` | finger count of the gesture |
| `Phase` | `Began` — the finger count is reached (neutral deltas) · `Changed` — a frame with a change · `Ended` — fingers lifted or the finger set changed |
| `Center` | finger centroid, px |
| `Pan` | centroid shift per frame, px — panning |
| `Spread` | mean finger distance to the center, px |
| `Scale` | `Spread` / previous frame's `Spread`: >1 — spreading, <1 — pinching (zoom, resolution-independent) |
| `Rotation` | finger rotation around the center per frame, degrees (counterclockwise is positive) |

A finger set change mid-gesture ends it (`Ended`) and immediately begins a new one (`Began`) — no jump of center or spread. The `Pan`/`Scale`/`Rotation` channels stay silent (neutral 0/1/0) until the change accumulated since the gesture start exceeds the threshold (`panThreshold` px, `scaleThreshold` fraction, `rotationThreshold` degrees); `Changed` is published only with a non-neutral delta. The frame is an impulse: one equal to the previous is published forcibly. Interpretation (camera zoom, map scroll, rotation) is up to the consumer.

**Recognized gestures → mask** — bindings `{ fingers (2–5), kind, action, moveCursorToCenter }`:
- **Tap** — from the first touch to the last finger lifted within `tapMaxDuration`, no finger moved further than `tapMaxMovement`; the finger count is the simultaneous maximum. The action is taken and released in one frame (a click — `VirtualCursorInputModule` does not lose the edges).
- **Hold** — the action is held while a gesture with exactly this finger count is in progress (a tap with the same finger count takes it briefly too).
- `moveCursorToCenter` — before taking the action the cursor moves to the gesture center (hiding per `hidesCursor`): the click lands where the fingers touched.

Actions are taken on behalf of the binding (holder set). `Disconnect` ends the gesture in progress and releases holds. A disabled action — no touches: the gesture ends, holds are released, an unfinished tap is dropped (no false tap).

### Driver tick
Drivers with `NeedsTick` are ticked by a self-rescheduling loop through `TimeController.Accumulate`. The inner `catch` isolates a failing driver, `finally` guarantees continuation; an exception is logged only for the first one in a streak, the counter resets on the first successful frame.

### Focus navigation
**Group stack.** On `OnEnable` a `FocusGroup` joins the stack respecting `Priority` (0..10): it rises to the top past groups with a strictly lower priority; equal priorities are LIFO. `ActiveGroup` is the first group from the top without `Ignore`; only its `Targets` take part in navigation. `OnDisable` removes the group, `OnDestroy` does final cleanup. A runtime `Priority` change applies on the next `OnEnable`. A runtime `Ignore` change notifies the controller: if the current focus ends up outside the active group it goes to its group's `RememberedFocus`; with no focus an automatic transfer is requested (in navigation mode), as on push/pop.

**Target registration.** `FocusTargetComponent` (`TargetKind.UGUI` — `RectTransform` center respecting canvas render mode; `TargetKind.World` — `Transform` projected through the active `CameraProvider` camera) finds the nearest parent `FocusGroup` on every `OnEnable` and registers; no group — `LogError` (anti-spam). `Unregister` clears the group's `RememberedFocus` only if the target was destroyed.

**Navigate.** A half-open cone `[-cone..+cone)` from the current focus point (no focus — from the cursor position); among candidates — minimum Euclidean distance; the current focus is excluded. `captureNearestIfFree`: with no focus and no candidates in the cone the nearest group target is taken. A focus transition: `NotifyUnfocused`/`NotifyFocused` (the target's UnityEvents), remembering in the groups' `RememberedFocus`, a position report with the hide flag and an OS mouse warp.

**Navigation mode** is a separate flag: a focus transition turns it on; only user cursor movement or an emptied group stack turns it off. It survives a temporary loss of focus (moved to `RememberedFocus` on push, the focused target disabled) — automatic focus transfer on push/pop/`Ignore` relies on it: the new active group gets its `RememberedFocus` or the target nearest to the cursor. The transfer is deferred to the next tick of the follow loop (`LateUpdate`): on the group's `OnEnable` its child targets are not registered yet, and a menu swap within one frame does not drop the mode on the intermediate empty stack. An explicit `Navigate` within this window cancels the deferred transfer. A significant cursor move (`PointerModel.OnPointerMoved`) ends the mode and clears focus; jitter and warp echo do not. The controller filters out its own cursor moves with a flag set during its own report.

**Model notifications.** `FocusModel.OnUpdateData` is batched through `TimeController.Accumulate`: a series of changes within one call (stack → focus cleared → new target) yields a single event in `LateUpdate` with the final state. `CurrentFocus.OnUpdate` stays synchronous.

**Follow.** A per-frame loop moves the cursor (and the OS mouse) to the target point if the target moved (scroll, layout, animation) — with the same hide flag.

**Submit.** `VirtualCursorFocusController.Submit()` emulates a left click at the target point as if a raycast hit the current focus `IFocusTarget.Target`: `PointerDown` bubbling up the hierarchy to the nearest press handler (without one the receiver is the nearest `IPointerClickHandler`), `PointerUp` to the press receiver, `PointerClick` to the nearest `IPointerClickHandler` up the hierarchy. Handlers that register a click by press/release/timing work, and so does a target below its handler (a button's tap zone). No focus — nothing.

**Focus handlers.** A focus change (`Navigate`) from `onFocused`/`onUnfocused` is rejected with `LogError`. Group stack and target registration changes from there (enabling/disabling groups and targets, `Ignore`) are not lost: they are deferred and run in order once the current controller operation finishes (not in the middle of a focus transition); each re-checks the scene when it runs — a push is skipped if the group is already disabled, a remove if it is enabled again.

`FocusCenterScrollRect` (on a `ScrollRect`) centers content on the focused element that is a descendant of `content`: along allowed axes, with the `axisTolerance` threshold relative to the previous focus, instantly or animated on unscaled time.

`FocusDropDownSelected` (on the `DropDownList` root, next to `FocusGroup` and `FocusCenterScrollRect`) moves focus to the selected item when the list opens in navigation mode. Without it the automatic focus transfer gives the list group the item nearest to the cursor (next to the dropdown button), not the current value. One frame after enabling (pool items are built and registered in the group by then) the handler finds the first enabled `DropDownItem` with `IsSelected` and passes its `FocusTargetComponent` to the internal `VirtualCursorFocusController.FocusTo`; scrolling to the item is done by `FocusCenterScrollRect` on the focus change. Order relative to the deferred automatic transfer within one wave does not matter: `FocusTo` cancels its request and stores the item in the group's `RememberedFocus`. Outside navigation mode (opened with the mouse) — nothing.

### Tweeners on keys (`PointerActionTweenerHandler`)
Globally subscribed to `Actions`: for each "action → `TweenerHub`" binding press → `Forward`, release → `Back`; on `OnEnable` tweeners are snapped to the current mask. A binding to `None` means "idle": it is active while none of the keys **bound in this handler** is pressed; unbound keys do not break idle. Its tweener (cursor idle) is `Forward` with nothing pressed, goes `Back` when a bound key is pressed and returns to `Forward` when the last bound key is released. The mask itself is unchanged: `None` is still never active in it.

Example — bindings `None → [idle]`, `BaseClick → [click]` (RMB not bound):

| Event | `[idle]` | `[click]` |
|---|---|---|
| Enable, nothing pressed | Forward (instant) | Back (instant) |
| LMB pressed | Back | Forward |
| LMB released | Forward | Back |
| RMB pressed (not bound) | stays Forward | stays Back |

Looping (endless idle) is configured on the `TweenerHub` itself — the handler only switches `Forward` / `Back`.

### Renderers
- **`UiCursorRenderer`** — the cursor RectTransform on an overlay canvas is placed at `ScreenPosition`; the skin is the `UIStateSwitcher` state with the same name (unknown name → state 0). Hiding needs a state named `hidden`. Position and skin are applied starting from the first position report: before it `OnEnable` sets the `hidden` state (state 0 if there is none); after it re-enabling applies the model's current position and skin right away. Optionally hides the system cursor.
- **`OsCursorRenderer`** — `Cursor.SetCursor` (ForceSoftware) from the skin's sprite set: the first set is the default skin, the `hidden` flag hides the OS cursor, the sprite is chosen by `Actions.Dominant()` (`actionsOverride[0..2]` with fallback). Redraws on `Skin` change. Requires a standalone sprite texture (not an atlas).

### Screen→world projection
LIFO camera registry (`CameraProvider` on OnEnable/OnDisable); `TryGetWorldHit`/`GetWorldProjection` — lazy `Physics.Raycast` from `ScreenPosition` cached per frame; the cache resets on cursor position and camera registry changes, manually — `InvalidateProjection` (scene geometry moved).

### Editor tools
- **`Tools/Vortex/Virtual Cursor/Focus Stack`** — group stack window: priority (`P<n>`), status (`▶ ACTIVE` / `⊘ IGNORED` / depth), `Targets` with markers `●` current focus, `◉` remembered, `[inactive]`; click — Ping + Select. Play Mode only; auto-repaint at most once per second, the tick is off outside Play Mode.
- **`Tools/Vortex/Configs/Input Driver Set`** — highlight `InputDriverSet` in Project.

---

## Contract

### Input
- `SdkSettings`: `cursorInputSdk` enabled (`USING_VORTEX_CURSOR`).
- `InputDriverSet` in `Resources/Settings`: a non-empty driver list with assigned action ids.
- Input Actions: actions for the drivers; maps are enabled by the project.
- `EventSystem` + `VirtualCursorInputModule` (instead of `InputSystemUIInputModule`) in UI scenes.
- A cursor renderer on an overlay canvas (`UiCursorRenderer`) or `OsCursorRenderer`.

### Output
- `PointerModel` (position / actions / skin / over-UI / scroll / last device) — reactive.
- UGUI events (hover/click/drag/scroll) at the cursor position from any source — through `VirtualCursorInputModule`; direct touches per platform.
- `FocusModel` (group stack, current focus) — reactive.
- `GestureModel` (multitouch gesture frame) — reactive.
- `RaycastHit`/projection point on request.

### Guarantees
- One screen position for rendering, UGUI input, focus and projection.
- Simultaneous actions; a single dominant one for the sprite.
- Source hiding takes priority over skin requests.
- The driver tick survives a failure of one driver.
- `ScreenPosition`/`Actions`/`Skin`/`IsOverUI`/`ScrollDelta`/`LastDevice`/`CurrentFocus`/`Gesture.Frame` are locked to the controllers.

---

## API

### VirtualCursorBus (static)
```csharp
const string HideCursor = "hidden";
static PointerModel Data;      // pointer runtime model
static FocusModel   Focus;     // focus navigation runtime model
static GestureModel Gesture;   // multitouch gesture runtime model (Frame)
```

### VirtualCursorController (static)
```csharp
// Skins (PointerModel extensions): VirtualCursorBus.Data.OnSkin(...)
static void OnSkin(this PointerModel model, string skin, Object skinOwner);
static void OffSkin(this PointerModel model, Object skinOwner);

// Projection
static bool     TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void     InvalidateProjection();

// internal (package drivers/components):
// ReportPointer(pos, hidesCursor, device) · ReportDevice(device) · SetAction(action, active, source) · ClearActions · SetOverUI
// ReportScroll · ReportGesture
// RegisterCamera / UnregisterCamera · ActiveCamera
```

### VirtualCursorFocusController (static)
```csharp
static bool IsReady;
static void Init();                                         // [RuntimeInitializeOnLoadMethod]
static void PushGroup(FocusGroup group);                    // from FocusGroup.OnEnable
static void RemoveGroup(FocusGroup group);                  // from OnDisable/OnDestroy
static void Navigate(Vector2 direction, float coneAngleDeg,
                     bool hideCursor, bool captureNearestIfFree);
static void Submit();                                       // click emulation (Down/Up/Click) on the current focus Target
```

### FocusGroup (MonoBehaviour)
```csharp
IReadOnlyList<IFocusTarget> Targets;
int  Priority;                         // 0..10
bool Ignore { get; set; }              // setter notifies the controller
IFocusTarget RememberedFocus;
void Register(IFocusTarget target);
void Unregister(IFocusTarget target);
void ClearRememberedFocus();
```

### IFocusTarget (contract)
```csharp
Vector2    ScreenPoint { get; }   // recomputed on every request
bool       IsActive    { get; }   // ready to take focus
void       NotifyFocused();
void       NotifyUnfocused();
GameObject Target      { get; }   // the "raycast hit" of the Submit click
```

### InputDriver (abstract, POCO)
```csharp
abstract void Connect();
abstract void Disconnect();
virtual  bool NeedsTick { get; }
virtual  void Tick(float unscaledDeltaTime);
virtual  bool HidesCursor { get; }
virtual  bool SupportsPlatform(RuntimePlatform platform);
// helpers: ResolveAction / SubscribeAction / UnsubscribeAction / Report(screen, device) / ReportDevice(device) / DeviceOf(action)
```

---

## Usage

### 1. Enable the module
In `SdkSettings` enable `cursorInputSdk` → **ApplyChanges** (define `USING_VORTEX_CURSOR`, recompile).

### 2. Configure InputDriverSet
`CoreAssetsController` auto-creates `Resources/Settings/InputDriverSet.asset`. Add drivers, assign action ids from the dropdown. For Android + Desktop — two `TouchInputDriver`s: `HideOnly` + `MobileOnly` (Button `<Touchscreen>/primaryTouch`) and `Delta` + `DesktopOnly` (Value `<Touchscreen>/delta`).

### 3. Input Actions
Actions for the drivers (mouse position, move vector, buttons, navigation directions, submit, scroll, touch). For `MultiTouchInputDriver` and `GestureInputDriver` — actions bound to touches as a whole: `<Touchscreen>/touch*/position`, and `<Touchscreen>/touch*/press` for finger buttons. Enable the virtual cursor map on the project side (e.g. an `InputMapHandler` in the persistent scene). `ActionInputDriver` button actions are of type **Button** (the action is set on `performed`, cleared on `canceled`); with PassThrough the release also arrives as `performed` and the action sticks. A UI click uses the same action: the package's UGUI module reads the action mask, no separate UI action is needed.

### 4. Scene
- On the `EventSystem` — `VirtualCursorInputModule` instead of `InputSystemUIInputModule` (`VirtualCursorBaseInput` is added automatically). For mobile — `nativeTouch` with `MobileOnly`.
- Overlay `Canvas` (Screen Space - Overlay, above all UI) + a cursor with a `UIStateSwitcher` (state 0 — default skin, `hidden` — hidden, others by skin names) + `UiCursorRenderer`. Disable raycast on the cursor graphics.
- Optional: `PointerActionTweenerHandler` on the cursor (press animation), `CameraProvider` on the camera, `CursorHoverZone` on UI elements (one zone — one raycast), `PointerActionHandler` for RMB/MMB.

### 5. Focus navigation (optional)
- In `InputDriverSet` — `UINavigationDriver`: 4 directions + submit, `coneAngleDeg`, `hideCursorOnFocus`, `captureFocusIfFree`.
- `FocusGroup` on the root of each navigation context (HUD, menu, dialog); switching contexts — `SetActive`.
- `FocusTargetComponent` on every focusable element inside a group; `onFocused`/`onUnfocused` — highlight/SFX. For a button, put `FocusTargetComponent` on the object with the click handler or below it (submit bubbles up the hierarchy like a raycast).
- `FocusCenterScrollRect` on `ScrollRect` lists.
- `FocusDropDownSelected` on the `DropDownList` root — focus on the selected item when opened.

---

## Known issues

| Issue | Where |
|---|---|
| `IsOverUI` is published but nothing in the package writes or reads it | `PointerModel` |
| `Skin` is not forwarded to `PointerModel.OnUpdateData` | `PointerModel` |
| `OsCursorRenderer` does not redraw on `Actions` changes; throws on empty `cursorSets`, `actionsOverride` shorter than three and `idle == null` | `OsCursorRenderer` |
| World target without a camera: `ScreenPoint` throws NRE, and with a focus present it is read without an `IsActive` check | `FocusTargetComponent`, `VirtualCursorFocusController` |
| `InputController` delivers `canceled` only to the action's top subscriber: if another one subscribes to the same action between press and release, the release never reaches the driver — the action sticks | `InputController` (Vortex core), `ActionInputDriver`, `UINavigationDriver` |
| `Example/` content lags behind the package (no `VirtualCursorInputModule`, old driver fields) | `Example/` |

---

## Edge cases

| Situation | Behavior |
|----------|----------|
| Module disabled | The package assembly does not compile; components in scenes become Missing Script |
| `InputDriverSet` missing / empty | `CursorInputLoader` throws |
| No driver in the set supports the current platform | `CursorInputLoader` throws |
| Driver does not support the platform | Not connected |
| Action map not enabled | No events, polling returns zero — the cursor does not move from that source |
| Touch in HideOnly / AbsolutePosition | Cursor hidden until a significant move from another source |
| HideOnly+Delta pair with `All` | Delta overrides hiding — split by platform |
| Touch with `nativeTouch` on the platform | Goes to UGUI directly (multitouch); the module's cursor part is not processed that frame |
| Touch without `nativeTouch` on the platform | Does not reach UGUI — drivers only |
| `MultiTouchInputDriver`: the action has no control on the finger | `LogError` once, the binding stays silent |
| `MultiTouchInputDriver` + `nativeTouch` on the same platform | The touch goes to UGUI directly, the driver's actions do not reach UGUI |
| `MobileOnly` in the Editor | Not connected |
| Exception in a driver `Tick` | Logged for the first in a streak; other drivers keep ticking |
| Skin not found by the renderer | State 0 (`UiCursorRenderer`) / first set (`OsCursorRenderer`) |
| No `hidden` state in `UiCursorRenderer` | Hiding shows the default skin |
| Two hover zones on one raycast | Skin request order undefined — invalid placement |
| No `FocusGroup` in the scene | `Navigate` does nothing |
| `FocusTargetComponent` without a parent group | `LogError`, the target is not registered |
| No candidates in the cone, focus present | Nothing (capturing the nearest — only without focus) |
| Cursor moved while focused | Significant (`OnPointerMoved`: step > 3 px or > 5 px from the drift center) — focus cleared; smaller — drift |
| Position report from a different source than the previous one | Not significant, the drift center moves to its point |
| OS mouse warp echo | Not significant (follows the warping source's report) — hiding, navigation mode and `LastDevice` unchanged |
| Slow stick (< 3 px per frame) with OS mouse warp | Does not raise `OnPointerMoved` (echo re-anchors the filter); `LastDevice` — the stick's device on every tick |
| Cursor move without a device (focus) | `LastDevice` unchanged |
| Focus target moved | The cursor follows once the gap exceeds 2 px |
| Submit without focus | Nothing |
| Focus change from `onFocused`/`onUnfocused` | Rejected with `LogError` |
| Enabling/disabling a group or target, `Ignore` from `onFocused`/`onUnfocused` | Deferred to the end of the controller operation and executed |
| Projection without a camera | Miss (`false`/`null`) |
| Atlas cursor sprite | `UiCursorRenderer` — fine; `OsCursorRenderer` — needs a standalone texture |

---

## File structure

```
VirtualCursorSystem/
├── Bus/VirtualCursorBus.cs
├── Controllers/
│   ├── VirtualCursorController.cs             # driver intake, skin resolution
│   └── VirtualCursorController.Projection.cs  # LIFO cameras + raycast
├── Model/
│   ├── PointerModel.cs  PointerAction.cs  PointerActionMask.cs  PointerActionMaskData.cs
│   ├── GestureModel.cs  GestureFrame.cs  GestureFrameData.cs
├── Focus/
│   ├── FocusModel.cs  FocusGroup.cs  FocusTargetData.cs  IFocusTarget.cs  VirtualCursorFocusController.cs
├── InputDrivers/
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs
│   ├── ScrollInputDriver.cs  TouchInputDriver.cs  MultiTouchInputDriver.cs  GestureInputDriver.cs  UINavigationDriver.cs
├── Handlers/
│   ├── CursorHoverZone.cs  CameraProvider.cs  FocusTargetComponent.cs  FocusCenterScrollRect.cs  FocusDropDownSelected.cs
│   ├── PointerActionHandler.cs  PointerActionTweenerHandler.cs
├── Render/
│   ├── UiCursorRenderer.cs  OsCursorRenderer.cs
├── UiModule/
│   ├── VirtualCursorInputModule.cs  VirtualCursorBaseInput.cs
├── DefineSettings/                            # SDK toggle (in the SdkSettings assembly via .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
├── Editor/
│   ├── FocusStackWindow.cs  MenuController.cs  VirtualCursorInputModuleProcessor.cs
├── Example/                                   # sample: Input Actions, InputDriverSet, cursor prefab, tween presets
└── ru.vortex.unity.virtualcursorsystem.asmdef # defineConstraints: ["USING_VORTEX_CURSOR"]
```
