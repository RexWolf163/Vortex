# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Assembly:** `ru.vortex.unity.virtualcursorsystem`

---

## Purpose

A multi-source virtual cursor for UGUI projects. A single screen position (`ScreenPosition`) is fed by any source — mouse, gamepad, keys, touch — and is the single source of truth. The position **lives in the model** (optionally the OS mouse is warped synchronously — `warpSystemMouse` toggle in `DirectInputDriver`).

Native UGUI (`Button`, `Toggle`, `ScrollRect`, hover, `IPointerXxx`) works without per-widget code: `VirtualPointerDispatcher`, on every cursor event, runs `EventSystem.RaycastAll` under `ScreenPosition` and sends standard `PointerDown`/`PointerUp`/`PointerClick`/`PointerEnter`/`PointerExit` via `ExecuteEvents`. There is no phantom `InputDevice`; UI Actions-asset bindings stay on `<Mouse>` — the physical mouse and the virtual cursor independently dispatch events to the same UGUI handlers.

Cursor appearance is a **render-agnostic skin system**: swappable theme sets (by key at runtime), resolution scaling (global tiers), sprite by action state, with upward fallback. Rendering goes through `ICursorRenderer` (default: a UGUI `Image` at the cursor position; optional: the OS cursor via `Cursor.SetCursor`).

**The whole package is gated by an SDK Settings toggle.** Sources are implemented as drivers (`InputDriver`) listed in the `InputDriverSet` config asset and connected at startup by the loader `CursorInputLoader`. The whole system is switched on via the `cursorInputSdk` toggle in `SdkSettings` (define `USING_VORTEX_CURSOR`): `defineConstraints` on the package asmdef. The only exception is the partial-file carrying the toggle itself (`DefineSettings/SdkSettings.CursorInput.cs`), which via `.asmref` lives in the `SdkSettings` assembly and is always visible. The cursor is a **supra-system entity**: there is no situational input gate — a connected driver is always active.

**Focus navigation** (`Focus/` subsystem): a `FocusGroup` on any parent gathers child `FocusTargetComponent`-s into a navigation context; groups live in a LIFO stack (push/pop on `OnEnable`/`OnDisable` — the standard SetActive-menu pattern). `UINavigationDriver` listens to 4 directional actions (D-pad / arrows); on press — the nearest target inside the semi-open ±45° cone **from the active (top) group** becomes active. The cursor is hidden via the external channel (`HideCursor`) and warped onto the target's point — hover and click go through the native UGUI path. Any cursor movement (mouse/stick) automatically clears the focus. When the active group changes (as long as the cursor stays in nav mode) focus is handed over automatically to the new group's `RememberedFocus` or to the nearest target.

**Contrast with `CursorSystem`:** `CursorSystem` — OS cursor + UGUI hover, mouse-only, the simplified alternative. `VirtualCursorSystem` — virtual cursor + source arbitration + render-agnostic swappable skins + a pluggable driver-based input layer.

Out of scope:
- Gameplay click triggers/mechanics — the consumer's level (`AdvancedButton`/game code); the package exposes position/actions/projection.
- Persistence of the selected theme — the project layer (L2 does not depend on L3 GameCore; see "Theme selection").
- Interpreting the world hit — the package returns a raw `RaycastHit`.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Unity.InputSystem` | `InputAction`, `Mouse` (warps the system mouse in `DirectInputDriver`) |
| `UnityEngine.UI` | `Image`/`Canvas` (UI render) |
| `UnityEngine.EventSystems` | `EventSystem.RaycastAll`, `ExecuteEvents`, `PointerEventData`, `IPointerXxx` interfaces |
| `Vortex.Unity.InputBusSystem` | `InputController` — resolves actions by string id "Map/Action", maps/subscription (LIFO) |
| `Vortex.Core.LoaderSystem` (apploader) | `IProcess`/`Loader` — connects drivers within the load pipeline |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — auto-provisions the `InputDriverSet` asset in `Resources/Settings` |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate` — per-frame driver tick |
| `Vortex.Sdk.SdkSettingsSystem` | module toggle + `DefineSymbol("USING_VORTEX_CURSOR")` |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `EnumData`/`StringData`/`BoolData` with owner protection |
| `Vortex.Unity.Extensions.ReactiveValues` | `Vector2Data` |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ClassLabel]`, `[ValueSelector]` (action-id dropdown) |
| Sirenix Odin Inspector | `[Tooltip]`, `[SerializeReference]`/`[HideReferenceObjectPicker]`, `[ToggleButton]` |

Input drivers do **not** use `InputActionProperty`: the binding is a string action id (the Vortex standard, as in `InputController`), resolved at runtime.

Theme persistence (`IGameData`) is implemented at the **project layer** (example: `_SexMusicIdol/_Scripts/UI/CursorSkinPersistence.cs`) — the package is save-agnostic.

---

## Architecture

```
[CursorSkinSettings] (SO)                       ← config: global tiers + theme catalog
 ├─ int[] resolutionTiers                        (Screen.height breakpoints, ASCENDING)
 ├─ string defaultSetKey
 └─ CursorSkinSet[] sets                          (theme = key + packs per tier)
      └─ CursorSkinPack[] tiers
           ├─ CursorSkin baseSkin                 (outside hover)
           └─ CursorSkin[] hoverSkins             (by string key)
                └─ CursorSkin { name, hideCursor, defaultSprite, CursorSpriteEntry[] overrides }

[PointerModel] (IReactiveData, runtime, NOT saved)
 ├─ Vector2Data ScreenPosition                    ← position truth
 ├─ EnumData<PointerSourceKind> ActiveSource      ← Analog/Point/Direct (last-source-wins)
 ├─ PointerActionMaskData Actions                 ← bitmask of simultaneous actions
 ├─ StringData HoverKey                           ← active hover skin
 └─ BoolData IsOverUI                             ← over UGUI (from EventSystem)

[VirtualCursorController] (static)
 ├─ Init(settings) / Cleanup() / RefreshResolution()
 ├─ ReportPointer(pos, source[, hidesCursor]) / SetAction / SetHover / SetOverUI   (internal — drivers)
 ├─ Recompute → CursorSkinResolver → Visual (CursorVisualData); mixes in hide-by-source
 └─ Projection: RegisterCamera(LIFO) + lazy raycast (TryGetWorldHit/GetWorldProjection)

[VirtualCursorBus] (static)  → Data / Visual / IsReady / OnReady            (read-only facade)
[CursorSkinSelector] (static) → Selected(StringData) / Select(key)          (save-agnostic)

Input layer (part of the package, gated with the whole assembly):
  [InputDriverSet] (SO, ICoreAsset)  → [SerializeReference] InputDriver[]      (Resources/Settings)
  [CursorInputLoader] (IProcess)     → Register in Loader · Resources.Load + failfast
                                        · connect per platform · tick via Accumulate (+anti-spam)
  [InputDriver] (POCO, abstract): Connect/Disconnect · NeedsTick/Tick · HidesCursor · SupportsPlatform
     ├─ MouseInputDriver (Analog)     · TouchInputDriver (Point; modes HideOnly/AbsolutePosition/Delta + platform filter)
     ├─ DirectInputDriver (Direct, NeedsTick; speedCurve + accelerationTime) · ActionInputDriver (buttons→mask)
     └─ UINavigationDriver (4 actions → VirtualCursorFocusController.Navigate)

Focus navigation (Focus/):
  [FocusModel] (IReactiveData)       → priority-ordered Groups[] + ActiveGroup (skips Ignored) + ReactiveValue<IFocusTarget> CurrentFocus
  [FocusGroup] (MonoBehaviour)        → OnEnable push with Priority bubble-insert · OnDisable pop · OnDestroy cleanup
                                        · Priority (0..10) · Ignore (reactive, notifies the controller)
                                        · Targets[] (children register) · RememberedFocus · FindNearestEuclidean
  [VirtualCursorFocusController] (static) → Init/Cleanup · PushGroup/RemoveGroup · Navigate(dir)/ClearFocus
                                            · auto focus transfer on push/pop if _focusAnchor != null
                                            · subscribes to ScreenPosition → auto-ClearFocus on movement
                                            · _focusAnchor absorbs sub-threshold drift (per-frame 3 px)
  [IFocusTarget] (contract)          → ScreenPoint · IsActive · NotifyFocused/NotifyUnfocused

Scene MonoBehaviours (not input drivers):
  CursorHoverZone (UGUI→HoverKey) · CameraProvider (projection camera, LIFO)
  PointerActionHandler (UGUI binding Action1/2/3 → UnityEvent — optional, on buttons)
  FocusGroup (on a parent; LIFO navigation context)
  FocusTargetComponent (on a button/object; UGUI/World → registers in the parent FocusGroup + UnityEvent onFocused/onUnfocused)

UGUI bridge:  VirtualPointerDispatcher — subscribes to ScreenPosition/Actions, RaycastAll, ExecuteEvents
Render:       ICursorRenderer → UiImageCursorRenderer (default) | OsCursorRenderer (opt.)
```

### Data flow

```
Source (mouse/stick/touch/keys)
   → InputDriver (resolves the action by id via InputController) → VirtualCursorController.ReportPointer/SetAction
        → PointerModel (ScreenPosition/Actions/HoverKey; hidesCursor by source)
             ├→ CursorSkinResolver → Visual → ICursorRenderer (draws the cursor)
             ├→ VirtualPointerDispatcher (OnUpdate→dirty; LateUpdate if dirty)
             │     → EventSystem.RaycastAll → ExecuteEvents(Enter/Exit/Down/Up/Click) → UGUI handlers
             └→ Projection (on demand) → RaycastHit
```

---

## Key concepts

### Input drivers as a pluggable module
- `InputDriver` — an abstract **POCO** (not a MonoBehaviour): `Connect()`/`Disconnect()`, `NeedsTick`/`Tick(dt)`, `HidesCursor`, `SupportsPlatform(platform)`. Actions are resolved by string id "Map/Action" via `InputController` (a `[ValueSelector]` dropdown in the inspector).
- `InputDriverSet` — an SO list of drivers (`[SerializeReference]`), `ICoreAsset` → auto-created at `Resources/Settings/InputDriverSet.asset`.
- `CursorInputLoader` — an `IProcess`: registers in `Loader`, in `RunAsync` loads the set from `Resources`, connects drivers for the current platform, starts the per-frame tick. **Failfast**: the module is on (`USING_VORTEX_CURSOR`) but the asset is missing or the list is empty → exception (no silent no-op).
- Switched on via the `cursorInputSdk` toggle in `SdkSettings` (define `USING_VORTEX_CURSOR`). With the define off, **the entire package assembly** does not compile (`defineConstraints` on the asmdef); the only exception is the partial-file carrying the toggle itself (`DefineSettings/SdkSettings.CursorInput.cs`), which via `.asmref` lives in the `SdkSettings` assembly and is always visible — otherwise it would be a chicken-and-egg (nothing to switch it on with). With the define off the package's components in scenes/prefabs become Missing Scripts — the expected Unity behavior, scenes/prefabs are not broken.
- **No input gate** — the cursor is supra-system: a connected driver is always active (no situational cut-off).

### Source arbitration (last-source-wins)
`ReportPointer(pos, source)` makes the reporting source active (last-source-wins). `PointerSourceKind`: `Analog` (mouse), `Point` (touch), `Direct` (gamepad/keys — velocity×dt integration, clamped to screen). The mouse jitter threshold from the old implementation is not carried into the new drivers (arbitration is pure last-source-wins).

### Hide cursor by source
A driver declares `HidesCursor` (on `TouchInputDriver` it depends on the mode: HideOnly/AbsolutePosition → true, Delta → false). The flag is passed through `ReportPointer(pos, source, hidesCursor)` and set on the controller by last-source-wins; `Recompute` mixes it over the resolver (`Hide = resolved.Hide || pointerHidden`). Switching sources returns the cursor correctly (mouse → visible again).

For "flag the source but don't touch the position" scenarios there is a dedicated intake `VirtualCursorController.SetActiveSource(source, hidesCursor)` — it updates `ActiveSource` + the hide channel without `ScreenPosition.Set`. Used by `TouchInputDriver` in **HideOnly** mode (we only need to hide the visual; UGUI clicks natively) — this avoids triggering `VirtualPointerDispatcher` with an extra raycast and conflicting with the native handler of the same device.

### TouchInputDriver: modes and platform filter
Touch has to be handled differently on different platforms. `TouchInputDriver` is a universal component with three modes and an explicit platform filter; the typical pattern is **two instances in one `InputDriverSet`**, split by platform.

**Modes (`TouchDriverMode`):**
- **HideOnly** (default). On touch sets `ActiveSource=Point` + `_pointerHidden=true` via `SetActiveSource`. `ScreenPosition` is NOT changed, `VirtualPointerDispatcher` is not triggered. The click is handled by the native `InputSystemUIInputModule` on `<Touchscreen>/primaryTouch`. Binding — any (Button `primaryTouch` or Value); the value is not read. **Needed on Android**.
- **AbsolutePosition**. The cursor jumps to the touch point — the original behavior. Binding — Value/Vector2 (`Touchscreen/primaryTouch/position`). Useful only where the native UGUI touch pipeline is disabled (kiosk).
- **Delta**. Trackpad — a finger shifts the cursor relatively; the cursor stays visible (`HidesCursor=false`). Binding — Value/Vector2 **delta** (`Touchscreen/delta`). The InputSystem zeroes the delta between touches by itself; the driver keeps no history. **Needed on desktop touchscreens**.

**Platform filter (`TouchPlatformFilter`):**
- **All** — any platform (default, backward compat; **not recommended for a paired set**).
- **MobileOnly** — Android/iOS runtime; does NOT connect in the editor (test on device).
- **DesktopOnly** — Standalone Windows/Mac/Linux + all editors.

**Why the filter.** In a paired set (HideOnly + Delta) on one platform there would be a last-source-wins conflict: Delta fires every frame of finger movement with `HidesCursor=false`, while HideOnly fires only once on tap-down with `HidesCursor=true`. During a swipe Delta would always overwrite hide to `false` — the cursor would be visible on Android too (undesired). The filter separates the instances: HideOnly → MobileOnly, Delta → DesktopOnly, no conflict.

### Driver tick (TimeController.Accumulate + anti-spam)
Drivers with `NeedsTick` (Direct) are ticked by a self-rescheduling loop via `TimeController.Accumulate` (no hidden runner). The loop is wrapped in `try/catch/finally`: the inner `catch` isolates a failing driver, `finally` guarantees continuation. Anti-spam: a driver exception is logged only on the **first** in a streak; the counter resets on the first clean frame. `Tick` runs on `unscaledDeltaTime` — it works during pause (menus) too.

### Focus navigation (Focus/)
Directional switching of the active element (D-pad / arrows) — an alternative to cursor control for gamepad / keyboard in UI scenes.

**Group stack (navigation contexts) — Priority + LIFO within a tier.**
- `FocusGroup` — a MonoBehaviour on any parent of interactive elements. On `OnEnable` it pushes onto the stack with bubble-insert by `Priority` (see below); on `OnDisable` it pops; on `OnDestroy` — final cleanup. The active group (`ActiveGroup`) is the nearest non-`Ignore` to the top; only its `Targets` participate in `Navigate`.
- The standard pattern is a SetActive menu: the HUD group is always active, when the pause menu opens it is pushed on top → navigation walks its buttons; close the menu and the HUD group becomes active again.
- Nested groups — nested `FocusGroup`s are allowed; a target binds to the nearest parent via `GetComponentInParent`.

**Priority (0..10).** On push the group bubbles toward the top past groups with **strictly lower** priority, stopping before the first equal-or-higher one. Within equal priorities — regular LIFO (later push on top). Example: `HUD(0)` + `Pause(5)` + `Toast(3)` yields order `[HUD, Toast, Pause]`, Pause remains active. `Priority=0` for all — behavior collapses to plain LIFO. Changing `Priority` at runtime does NOT re-order already-registered groups — it applies on this group's next `OnEnable`.

**Ignore (reactive).** A `bool` toggle on the group: when `true` the group **stays in the stack** (targets live, `RememberedFocus` is kept) but `ActiveGroup` skips it. The setter is idempotent and notifies the controller (`VirtualCursorFocusController.OnGroupIgnoreChanged`): if, after the Ignore change, `CurrentFocus` ends up outside the active group — an immediate transfer kicks in (saved to the owning group's `RememberedFocus` + `NotifyUnfocused` + auto-focus in the new active in nav-mode). If the flag was flipped on a group deep in the stack and `ActiveGroup` did not change — no-op, no focus flicker.

**Target registration.**
- `FocusTargetComponent` on a UGUI button (`TargetKind=UGUI`, reference to a `RectTransform`) or world object (`TargetKind=World`, `Transform` + an active camera from `CameraProvider`). `OnEnable` → `GetComponentInParent<FocusGroup>` + re-resolve of the parent canvas → `Register`; `OnDisable` → `Unregister` from the same group it was registered in.
- **Re-resolve on every OnEnable, not lazy.** Reparenting the target between OnDisable and OnEnable (UI pooling, dynamic menu composition, `DontDestroyOnLoad move`) is handled correctly — the old registration is removed in OnDisable, the new one is attached to the actual current parent group. The cost of `GetComponentInParent` on rare OnEnables is negligible.
- **Fail-loud:** a target with no `FocusGroup` in its parents → `LogError` (anti-spam: successive null-resolves stay silent, the flag is cleared on the first resolve that finds one). That is a scene setup error.

**Navigate algorithm.** Semi-open cone `[-45°..+45°)` relative to the direction (4 directions × 90° = 360° coverage without overlap, each target lands in exactly one zone). Among candidates — **minimum Euclidean distance**. Origin is the **exact `ScreenPoint` of the current focus** (not the cursor position — after `WarpCursorPosition` the OS rounds the mouse to an int pixel, giving a ≈0.5 px drift that breaks cone selection at short distances); if there is no focus, fallback to the cursor's `ScreenPosition`. The current `CurrentFocus` is excluded from candidates (a defensive `ReferenceEquals` on top of the distance filter).

**Focus capture (`captureFocusIfFree`).** An optional fallback mode in `UINavigationDriver` (default `true`): if there is nobody in the cone **and** `CurrentFocus == null` — pick the nearest active target of the group by Euclidean distance, ignoring direction. Any directional key becomes an entry point into nav mode: the cursor "snaps" to the nearest button. When focus is already active this fallback is **not** applied — otherwise navigation at the edge of a list (no neighbors in that direction) would silently jump to the opposite end.

**Auto focus transfer on push/pop.** Triggered **only** when `_focusAnchor != null` (the cursor was "pinned" by navigation and has not been moved by the mouse since):
- Push: the old focus is saved as the old group's `RememberedFocus` + `NotifyUnfocused`; in the new top group the focus becomes `RememberedFocus` (if still active) or the nearest target by Euclidean distance → `NotifyFocused` + warp.
- Pop: symmetric — the old group loses its current (saved into its Remembered), the new top group restores its own.
- When `_focusAnchor == null` (the user moved the mouse and left nav mode) — the automation is disabled, the stack just rearranges, the cursor stays free.

**Focus effects:** `IFocusTarget.NotifyFocused`/`NotifyUnfocused` → UnityEvents on the component (highlight / SFX); the cursor is hidden via `VirtualCursorController.HideCursor()` (external channel — independent of driver Reports); `Mouse.WarpCursorPosition` syncs the OS mouse with the target point.

**Auto-ClearFocus on cursor movement:** `_focusAnchor` + `AnchorToleranceSqr=9` (3 px) — a **per-frame** threshold, not cumulative. A sub-threshold delta is **absorbed into the anchor** on every `ScreenPosition` update: OS mouse hardware noise (~1 px/frame at rest), int rounding of `WarpCursorPosition`, and warp echoes do not accumulate frame after frame against the original anchor — otherwise a random spike would cross the tolerance within a second or two without any user action (symptom: "periodic drops into free cursor"). A deliberate mouse gesture (5+ px/frame even on the slowest deliberate motion at 60 Hz) crosses the threshold in one frame → `ClearFocus` + `ShowCursor()` + nav mode exits.

### External cursor-hide channel (`HideCursor`/`ShowCursor`)
Independent API on top of sources: `VirtualCursorController.HideCursor()` / `ShowCursor()`. `_externalHidden` participates in the OR composition of `visual.Hide` inside `Recompute` (three channels: skin → `_pointerHidden` from the source → `_externalHidden` from external code). It is **not** reset by driver `ReportPointer`s — critical for focus navigation, where otherwise the mouse echo after a warp would overwrite hide back to false.

`ShowCursor` clears **only** its own channel: if the skin was authored with `HideCursor=true` or the active source (`TouchInputDriver`) hides the cursor — it stays hidden.

### Speed profile in DirectInputDriver (curve + acceleration)
Final cursor speed: `speed × speedCurve.Evaluate(stickMagnitude) × accelFactor`.
- **`speedCurve`** (AnimationCurve) — nonlinear response to stick deflection. X ∈ [0..1] = stick magnitude, Y = multiplier to `speed`. Default — constant 1 (curve has no effect). For precision control at small deflections — `Pow(x, 2)` or similar.
- **`accelerationTime`** (seconds) — smooth ramp from 0 to maximum speed when the stick leaves the deadzone. `0` = instant ramp (bang-bang, previous behavior). Braking inside the deadzone is **instant** (`_accelFactor` resets to 0) — the cursor follows the designer with no stop inertia; the next start begins from 0.
- **Validation**: on `Connect` checks `speedCurve.Evaluate(1) ≈ 0` → `LogWarning` with the `moveActionId`. Catches the common misconfiguration "cursor doesn't move at full stick deflection".

### Action mask (simultaneity + dominant)
`PointerAction` — a sequential index enum (`None` + `Action1…Action10`; convention: 1=LMB, 2=RMB, 3=MMB, 4=Back, 5=Forward, 6=Scroll↑, 7=Scroll↓, 8–10=reserve). `PointerActionMask` — a `readonly struct` over `int`: bits = simultaneously active actions; `Dominant()` — the lowest active bit by priority (for the sprite). `ActionInputDriver` sets/clears bits on `started`/`canceled`; `canceled` on alt-tab clears them by itself.

### Skins: theme → tier → hover → action, with upward fallback
`CursorSkinResolver.Resolve`:
1. **Theme** — `CursorSkinSelector.Selected` → `CursorSkinSet` (default if not found).
2. **Resolution tier** — `SelectTierIndex(Screen.height)`: smallest `resolutionTiers[i] >= height`, else the largest → `CursorSkinPack`.
3. **Skin** — hover skin by `HoverKey`, else the base; `HideCursor` → cursor hidden.
4. **Sprite** — by `Actions.Dominant()`: skin `override` → its `defaultSprite` → **up**: the pack's base skin → its `defaultSprite`.
5. Hotspot — from `Sprite.pivot`, Y-inverted.

### Global resolution tiers
Breakpoints (`resolutionTiers`) are defined **once** in `CursorSkinSettings`; each theme provides one pack per tier (`OnValidate` warns on mismatch). On a resolution change → `VirtualCursorController.RefreshResolution()`.

### Initial-flash: first-report gate in the UGUI renderer
`UiImageCursorRenderer` keeps `image.enabled = false` until the first real position report from any driver (the `_firstReportReceived` gate). Otherwise the theme's default sprite would flash at `(0, 0)` on scene load — on touch platforms this is a flash in the corner of the screen until the first touch. The gate is released inside `OnPosition`; `OnVisual` with the gate still closed does not draw either (theme/action changes while in the initial state don't open the visual). On `OnEnable` the gate is reset — a re-enable of the renderer starts clean.

Important consequence for Android: if the device has no mouse/gamepad and the only input source is `TouchInputDriver` in HideOnly mode, `ScreenPosition` never changes (the mode uses `SetActiveSource` without `Set` on the position). The gate is never released and the visual stays hidden **forever** — exactly what you want on a pure touch device.

### Virtual pointer and native UGUI (`VirtualPointerDispatcher`)
`VirtualPointerDispatcher` subscribes to `ScreenPosition.OnUpdate` and `Actions.OnUpdate` on the `Bus`; an event sets `_dirty` — `LateUpdate` runs the cycle **only** on changes (idle cost — one boolean check). In one pass: `EventSystem.RaycastAll` at the position → Enter/Exit diff → Action1/2/3 (LMB/RMB/MMB) transitions via `pointerDownHandler`/`pointerUpHandler`/`pointerClickHandler` through `ExecuteEvents` on the found target. Standard UGUI click canon (Up on the same `IPointerClickHandler` target as Down) is preserved.

**No phantom `InputDevice`**: UI Actions-asset bindings stay on `<Mouse>` for the physical mouse. The virtual pointer and the physical mouse dispatch events to the same UGUI handlers independently — no loops. Works with UGUI, 2D-/3D-colliders (via standard `Physics2DRaycaster`/`PhysicsRaycaster` on the camera — `RaycastAll` returns those as well).

**Lazy initialization**: `PointerEventData` is created inside `LateUpdate` on the first appearance of `EventSystem.current`. The dispatcher works correctly on a `Preload` scene when the EventSystem lives in a UI scene that loads later.

**On buttons**: for the **right/middle** button — `PointerActionHandler` on a UGUI element (`PointerAction` dropdown + `onPressed`/`onReleased`/`onClick`). For the **left** — the regular `Button.onClick`. Action4..Action10 don't pass through UGUI pipeline (`PointerEventData.InputButton` knows only Left/Right/Middle).

### IsOverUI
`VirtualPointerDispatcher` writes `PointerModel.IsOverUI` from its own `RaycastAll` at the virtual cursor position (whether the raycast hit UI) — world-projection consumers gate the click on this flag. The source is the virtual cursor itself, not `EventSystem.IsPointerOverGameObject()` (which would reflect the physical mouse). No separate component is needed.

### Screen→world projection
`VirtualCursorController` keeps a LIFO camera registry (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — a lazy `Physics.Raycast` cached per frame. The package returns the raw hit.

### Theme selection (save-agnostic)
`CursorSkinSelector` holds the reactive theme key (`Selected`) and `Select(key)`. Persistence (`IGameData`) is at the **project layer**. The package (L2) does not depend on GameCore (L3).

### Editor tooling
- **`Tools/Vortex/Virtual Cursor/Focus Stack`** — diagnostic window: the `FocusGroup` stack top-to-bottom (top = top of stack, `▶ ACTIVE` — the nearest non-Ignored to the top), per-group priority chip on the left (`P<n>`), `⊘ IGNORED` marker on Ignored groups (grey italic), list of `Targets`, `●` marker on `CurrentFocus`, `◉` on `RememberedFocus`, `[inactive]` tag. Click a group or target row — Ping + Select in Hierarchy. Data appears only in Play Mode after `VirtualCursorBootstrap.Init`; auto-repaint via `EditorApplication.update`. Style palette switches by `EditorGUIUtility.isProSkin`.
- **`Tools/Vortex/Configs/Virtual Cursor Skin Settings`** — ping the `CursorSkinSettings` asset in the Project window.
- **`Tools/Vortex/Configs/Input Driver Set`** — ping the `InputDriverSet` asset in the Project window.
- The legacy `Tools/Vortex/Configs/Cursor Settings` entry (from the `CursorSystem` package) is wrapped in `#if !USING_VORTEX_CURSOR` — hidden from the menu when the new cursor is on.

---

## Contract

### Input
- `SdkSettings`: the `cursorInputSdk` toggle is on (define `USING_VORTEX_CURSOR`).
- `InputDriverSet` (SO in `Resources/Settings`): a non-empty list of drivers with assigned action ids.
- Input Actions: actions for the drivers (mouse position, touch position, move vector, buttons Action1…Action10); the UI Actions-asset (`InputSystemUIInputModule`) stays standard — binds to `<Mouse>`.
- `CursorSkinSettings` (SO) passed to `Init` (via `VirtualCursorBootstrap`).
- `EventSystem` in the active scenes (standard UGUI object, required by the dispatcher).

### Output
- `PointerModel` (position/source/mask/hover/over-UI) — reactive.
- `CursorVisual` — the current cursor look (sprite+hotspot+hide) for renderers.
- Standard UGUI events on targets under the cursor (Enter/Exit/Down/Up/Click) via `VirtualPointerDispatcher`.
- `RaycastHit`/projection point on demand.

### Guarantees
- One screen position for render, UI and projection — no desync.
- Simultaneous actions on the device; a single dominant for the sprite.
- `canceled`/alt-tab clears action bits — no stuck buttons.
- The driver tick survives one driver's failure (try/catch/finally, log without spam).
- Ownership of reactive fields is bound to the controller — not writable from outside.

### Limitations
- The package requires the `USING_VORTEX_CURSOR` define (`defineConstraints` on the asmdef); with it off no type of the package compiles — external code that references `VirtualCursorBus`/`FocusGroup`/etc. also fails to compile unless it gates its own call.
- `InputDriverSet` must exist and be non-empty — otherwise `CursorInputLoader` throws (failfast).
- `VirtualPointerDispatcher` must be **in an active scene** (persistent scene next to Bootstrap/Renderer is ideal); without it UGUI handlers get no events from the virtual cursor. The `EventSystem` can live on any other scene — the dispatcher attaches to it lazily.
- Only `Action1`/`Action2`/`Action3` (LMB/RMB/MMB) reach UGUI handlers. For `Action4..Action10` a direct binding on `VirtualCursorBus.Data.Actions.OnUpdate` with a manual hover check is required.
- Projection requires a registered camera; without one — a miss.
- `OsCursorRenderer` requires a **standalone texture** for the sprite (`Cursor.SetCursor` takes a whole `Texture2D`). For atlased cursors use `UiImageCursorRenderer`.
- `InputController` (the input bus) must be available at connect time — it lazily initializes on first access (`GetAction`), so no explicit wait is needed in `WaitingFor`.

---

## API

### VirtualCursorBus (static)
```csharp
static PointerModel     Data;      // runtime model
static CursorVisualData Visual;    // current cursor look
static FocusModel       Focus;     // runtime registry of focus navigation (CurrentFocus.OnUpdate)
static bool             IsReady;
static event Action     OnReady;
```

### VirtualCursorController (static)
```csharp
static void Init(CursorSkinSettings settings);
static void Cleanup();
static void RefreshResolution();
static void ConfigureProjection(LayerMask mask, float distance);
static bool TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void InvalidateProjection();

// External cursor-hide channel — independent of Reports and the skin (OR composition).
static void HideCursor();                 // _externalHidden = true, Recompute
static void ShowCursor();                 // _externalHidden = false; the skin/source stay
static bool IsCursorHiddenExternally;     // query

// intake (internal): ReportPointer(pos,src) / ReportPointer(pos,src,hidesCursor)
//                    / SetActiveSource(src, hidesCursor)    ← source+hide WITHOUT ScreenPosition.Set
//                    / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### VirtualCursorFocusController (static)
```csharp
static FocusModel Model;                                         // null until Init
static bool IsReady;

static void Init();                                              // called by VirtualCursorBootstrap
static void Cleanup();
static void PushGroup(FocusGroup group);                         // from FocusGroup.OnEnable; bubble-insert by Priority
static void RemoveGroup(FocusGroup group);                       // from OnDisable/OnDestroy
static void Navigate(Vector2 direction, float coneAngleDeg,
                     bool hideCursor, bool warpSystemMouse,
                     bool captureNearestIfFree);                 // from UINavigationDriver
static void ClearFocus();                                        // + ShowCursor() + NotifyUnfocused
internal static void OnGroupIgnoreChanged(FocusGroup group);     // from FocusGroup.Ignore setter
```

### FocusGroup (MonoBehaviour)
```csharp
IReadOnlyList<IFocusTarget> Targets;                             // registered by children
int  Priority;                                                   // 0..10 Range slider; bubble-insert on PushGroup
bool Ignore { get; set; }                                        // reactive: setter triggers OnGroupIgnoreChanged
IFocusTarget RememberedFocus;                                    // persists between activations
void Register(IFocusTarget target);                              // from FocusTargetComponent.OnEnable
void Unregister(IFocusTarget target);                            // from OnDisable
void ClearRememberedFocus();                                     // explicit reset (optional)
```

### IFocusTarget (contract)
```csharp
Vector2 ScreenPoint { get; }   // recomputed on every query
bool IsActive { get; }         // GameObject.activeInHierarchy + local gates
void NotifyFocused();          // the controller calls when focus transitions onto this target
void NotifyUnfocused();        // another target / ClearFocus / OnDisable
```

### CursorSkinSelector (static)
```csharp
static StringData Selected;                 // reactive theme key
static void Select(string setKey);
static bool IsSelected(string setKey);
```

### InputDriver (abstract, POCO)
```csharp
abstract void Connect();
abstract void Disconnect();
virtual  bool NeedsTick { get; }            // Direct → true
virtual  void Tick(float unscaledDeltaTime);
virtual  bool HidesCursor { get; }          // Point/Touch → true
virtual  bool SupportsPlatform(RuntimePlatform platform);
// helpers: ResolveAction / EnableMap / DisableMap / SubscribeAction / UnsubscribeAction / Report
```

### InputDriverSet (SO, ICoreAsset) / CursorInputLoader (IProcess)
```csharp
InputDriver[] InputDriverSet.Drivers;       // Resources/Settings/InputDriverSet.asset
// CursorInputLoader: Register→Loader, RunAsync(load+failfast+connect+tick), WaitingFor()=empty
```

---

## Usage

### 1. Enable the input module
In the `SdkSettings` asset toggle `cursorInputSdk` → **ApplyChanges** (adds the `USING_VORTEX_CURSOR` define, recompiles).

### 2. Configure the InputDriverSet
`CoreAssetsController` auto-creates `Resources/Settings/InputDriverSet.asset` (or `Tools/Vortex/Debug/Check Core Assets`). Add drivers (`MouseInputDriver`/`TouchInputDriver`/`DirectInputDriver`/`ActionInputDriver`), assign action ids from the dropdown. An empty set → failfast on Play.

For a multi-platform build (Android + Desktop) — **two instances of `TouchInputDriver`**:
1. `mode=HideOnly`, `platformFilter=MobileOnly`, binding — Button `<Touchscreen>/primaryTouch`.
2. `mode=Delta`, `platformFilter=DesktopOnly`, binding — Value `<Touchscreen>/delta`.

On Android the first one is picked (cursor is hidden, native UGUI handles the click). On Desktop/in the editor — the second one (the finger acts like a trackpad). See the "TouchInputDriver: modes and platform filter" section.

### 3. Skin config
`Create → Vortex/UI/Cursor Skin Settings`. Fill `resolutionTiers` (ascending), `defaultSetKey`, `sets` — themes; in each theme — packs per tier, base/hover skins, `defaultSprite` + sparse `overrides` (action→sprite).

### 4. Input Actions
Actions for the drivers (mouse position, stick move, touch, buttons Action1…Action10). **The UI Actions-asset for `InputSystemUIInputModule` stays standard** — `Point/Click/RightClick/ScrollWheel` bind to `<Mouse>` (physical mouse). No rebind to a virtual pointer is required — its events go straight through `ExecuteEvents`.

### 5. Scene
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, projection params) — on a persistent scene (`Preload`/boot). `VirtualCursorFocusController.Init` is called automatically from `Awake`.
- `VirtualPointerDispatcher` — there too, next to the Bootstrap. **Input drivers are not placed on the scene** — they live in the `InputDriverSet`.
- An overlay `Canvas` (Screen Space - Overlay, above all UI) + a cursor `Image` (Raycast Target off) + `UiImageCursorRenderer`.
- Optional: `CameraProvider` (on the camera), `CursorHoverZone` (on interactive UGUI elements, hover-skin key), `PointerActionHandler` (on UI buttons for RMB/MMB), `FocusTargetComponent` (on UI buttons or world objects for gamepad navigation).
- For 2D/3D objects — `Physics2DRaycaster`/`PhysicsRaycaster` on the camera + a MonoBehaviour with `IPointerClickHandler`/`IPointerEnterHandler` on the object. The dispatcher works uniformly for UGUI and world colliders.

### 7. Focus navigation (optional)
- In the `InputDriverSet` add a `UINavigationDriver`: assign 4 action ids for the directions (Gamepad D-pad / Keyboard arrows), set `coneAngleDeg = 45`, `hideCursorOnFocus = true`, `warpSystemMouse = true`, `captureFocusIfFree = true` (when focus is empty and nothing is in the cone — capture the nearest target of the group; a convenient entry point into nav mode).
- On the parent of interactive elements (usually the Canvas or a menu container) — a `FocusGroup`. One component per navigation context (HUD, pause menu, modal dialog — each its own).
- On every focusable element inside that group's hierarchy — a `FocusTargetComponent`:
  - `TargetKind=UGUI` + reference to the `RectTransform` (the `rectTarget` slot is visible in the inspector only for UGUI via the Odin `ShowIf`).
  - `TargetKind=World` + reference to the `Transform` (the `worldTarget` slot) — requires a registered `CameraProvider` camera to compute the screen point.
- Wire the `onFocused`/`onUnfocused` UnityEvents for highlights/SFX.
- Context switching uses the standard Unity pattern: `SetActive(true)` on an object with a `FocusGroup` → push onto the LIFO, it becomes active. `SetActive(false)` → pop, the previous group becomes active again. In nav mode (`_focusAnchor != null`) focus is handed over automatically — to the new group's `RememberedFocus` or to the nearest target.
- Clicking a focused element works automatically — the cursor is already at the point via the warp, `VirtualPointerDispatcher` sends `PointerClick` to native UGUI handlers.

### 6. Theme persistence (project layer)
`CursorSkinData : IGameData` + a mirror: on load/new-game `CursorSkinSelector.Select(data.SelectedSetKey)`, on `Selected.OnUpdate` write it back.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Module off (`USING_VORTEX_CURSOR` off) | The entire package assembly does not compile (`defineConstraints`); types are unavailable, components in scenes/prefabs become Missing Scripts (scenes/prefabs are not broken) |
| `InputDriverSet` missing / empty | `CursorInputLoader` throws (failfast on load) |
| Driver doesn't support the platform | Skipped at connect (`SupportsPlatform`) |
| Active source is touch (`Point`), `TouchInputDriver` in HideOnly/AbsolutePosition | Cursor hidden (`HidesCursor=true`); mouse/gamepad show it again |
| `TouchInputDriver` in Delta on Desktop + swipe | `Report(cursor+delta, Point, HidesCursor=false)`; cursor VISIBLE, follows the finger proportionally (trackpad) |
| `TouchInputDriver` in HideOnly on Android + button tap | `SetActiveSource(Point, true)` → cursor hidden, `ScreenPosition` unchanged → `VirtualPointerDispatcher` not triggered → the native `InputSystemUIInputModule` handles the click, no double |
| HideOnly+Delta pair with `platformFilter=All` | last-source-wins conflict: Delta fires every swipe frame with `HidesCursor=false` and overwrites HideOnly → cursor visible everywhere. Separate by MobileOnly/DesktopOnly |
| `TouchPlatformFilter=MobileOnly` in the editor | Does NOT connect (`Application.platform` in the editor is always `*Editor`); test on device |
| Pure touch device (no mouse/gamepad), HideOnly only | `ScreenPosition` never changes (mode uses `SetActiveSource`) → `_firstReportReceived` in the renderer is never released → the visual stays hidden permanently (expected for pure touch) |
| Exception in a driver's `Tick` | Logged only on the first in a streak; the loop lives, other drivers tick |
| `CursorSkinSettings` not passed to `Init` | `Visual` = None; cursor not drawn |
| Theme key not found | Default (`defaultSetKey`), else the first |
| Resolution above all tiers | Largest tier; below all — the smallest |
| Action without a sprite in a skin | Fallback up: base skin → its `defaultSprite`; nowhere → None |
| Skin with `HideCursor` | Cursor hidden, no sprite applied |
| `VirtualPointerDispatcher` missing in the scene | UGUI doesn't react to the virtual cursor (physical mouse keeps working via InputSystemUIInputModule) |
| `DirectInputDriver.speedCurve.Evaluate(1) == 0` | `LogWarning` on Connect; cursor won't move at full stick deflection — fix the curve |
| `DirectInputDriver.accelerationTime = 0` | Instant ramp (bang-bang) — original behavior before the change |
| No `FocusGroup` in the scene | `UINavigationDriver.Navigate` finds nothing (empty stack) — stays silent |
| `FocusTargetComponent` with no parent `FocusGroup` | `LogError` on first Enable, the component is not registered — fix the scene |
| `FocusTargetComponent` with `TargetKind=World` and no camera in `CameraProvider` | `IsActive = false` → target excluded from selection |
| Deactivating the active `FocusGroup` (`SetActive(false)`) | `OnDisable` → `RemoveGroup` → in nav mode: auto-transfer focus to the new top group (RememberedFocus or nearest); otherwise the stack just rearranges |
| Activating another `FocusGroup` on top | `OnEnable` → `PushGroup` → in nav mode: auto-transfer focus into the new group; otherwise — the old focus is dropped (NotifyUnfocused), the new group has no focus |
| Returning to a previously active group | Its `RememberedFocus` is restored as the focus (if still active); otherwise — the nearest Euclidean target |
| `FocusTargetComponent` deactivated while it is `CurrentFocus` | `OnDisable` → `Unregister` on the group (clears its `RememberedFocus` if it pointed here); the global `CurrentFocus` clears via ClearFocus on the next Navigate |
| User moved mouse/stick while focus is active | Auto-`ClearFocus` on a frame whose per-frame delta exceeds `AnchorToleranceSqr` (3 px); sub-threshold deltas are absorbed into the anchor and don't accumulate. Focus removed, cursor becomes visible, nav mode is exited (auto-transfer on push/pop no longer fires) |
| Nobody in the cone, `CurrentFocus == null`, `captureFocusIfFree = true` | Focus capture fallback: the nearest active target of the group by Euclidean distance, direction ignored |
| Nobody in the cone, `CurrentFocus != null` (focus active, edge of the list) | Stays silent — the capture fallback is intentionally skipped (so navigation doesn't jump to the opposite end) |
| `HideCursor` + skin with `HideCursor=false` + `ShowCursor` | `_externalHidden` cleared, the skin shows the cursor again (if `_pointerHidden` is also false) |
| Nested `FocusGroup` | A target binds to the nearest parent via `GetComponentInParent`; the stack works naturally: nested on top of its parent |
| `FocusGroup.Priority` = 10, no other high priorities | On `PushGroup` the group bubbles to the top, becomes `ActiveGroup` regardless of its push position in LIFO |
| `FocusGroup.Priority` changed at runtime on an already-registered group | The stack is NOT re-ordered — ordering applies on this group's next OnEnable (SetActive false→true re-pushes it with the new priority) |
| `FocusGroup.Ignore = true` at runtime on the active group | Setter triggers `OnGroupIgnoreChanged` → `CurrentFocus` is moved into its `RememberedFocus`, `NotifyUnfocused` → highlight goes down, auto-focus into the new `ActiveGroup` in nav-mode |
| `FocusGroup.Ignore = true` on a group deep in the stack (not active) | `ActiveGroup` did not change — `OnGroupIgnoreChanged` detects this and no-ops, no focus flicker |
| `FocusGroup.Ignore = false` on a group whose priority will shadow the current active | `OnGroupIgnoreChanged` detects the ActiveGroup change → focus transfer into it (RememberedFocus or nearest) |
| Reparenting of `FocusTargetComponent` between OnDisable and OnEnable | On the next OnEnable `_group` is re-resolved via `GetComponentInParent` → correctly registered in the new parent group (lazy cache removed, re-resolve on every Enable) |
| `EventSystem` not yet loaded on start | The dispatcher attaches lazily when the EventSystem appears; events before that are lost (the cursor isn't over UI yet) |
| Alt-tab with a button held | `canceled` clears the bit — no stuck state |
| Dispatcher disabled (`OnDisable`) | All lingering Enter/Press are released; the next Enable starts clean |
| Projection without a registered camera | Miss (`false`/`null`) |
| Atlased cursor sprite | `UiImageCursorRenderer` — OK; `OsCursorRenderer` — needs a standalone texture |

---

## File Structure

```
VirtualCursorSystem/
├── Bus/VirtualCursorBus.cs
├── VirtualCursorController.cs            # static core: model, Visual resolve, intake, hide-by-source
├── VirtualCursorController.Projection.cs # LIFO cameras + raycast
├── VirtualCursorBootstrap.cs             # Init + ConfigureProjection
├── Model/
│   ├── PointerAction.cs  PointerSourceKind.cs
│   ├── PointerActionMask.cs  PointerActionMaskData.cs
│   ├── CursorVisual.cs  CursorVisualData.cs  PointerModel.cs
│   ├── CursorSkinResolver.cs  CursorSkinSelector.cs
├── Config/
│   ├── CursorSpriteEntry.cs  CursorSkin.cs  CursorSkinPack.cs
│   ├── CursorSkinSet.cs  CursorSkinSettings.cs
├── Input/
│   ├── VirtualPointerDispatcher.cs  PointerActionHandler.cs
├── Focus/
│   ├── FocusModel.cs  FocusGroup.cs  FocusTargetData.cs  IFocusTarget.cs  VirtualCursorFocusController.cs
├── InputDrivers/                         # input driver layer (gated with the whole assembly)
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  TouchInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs  UINavigationDriver.cs
├── Drivers/                              # MonoBehaviour, scene-bound (not input drivers)
│   ├── CursorHoverZone.cs  CameraProvider.cs  FocusTargetComponent.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
├── DefineSettings/                       # SDK toggle (folded into the SdkSettings assembly via .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
├── Editor/                               # editor-only (standard Unity folder, no own asmdef)
│   ├── FocusStackWindow.cs               # Tools/Vortex/Virtual Cursor/Focus Stack — LIFO dump + Ping
│   ├── MenuController.cs                 # Tools/Vortex/Configs/Virtual Cursor Skin Settings + Input Driver Set
└── ru.vortex.unity.virtualcursorsystem.asmdef  # defineConstraints: ["USING_VORTEX_CURSOR"]
```

Theme persistence (`CursorSkinData : IGameData` + mirror) lives at the project layer, outside the package.
