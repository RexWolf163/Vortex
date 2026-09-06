# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Assembly:** `ru.vortex.unity.virtualcursorsystem`

---

## Purpose

A multi-source virtual cursor for UGUI projects. A single screen position (`ScreenPosition`) is fed by any source — mouse, gamepad, keys, touch — and is the single source of truth. The position is **decoupled from the OS mouse**: it lives in the model, not in the Mouse device, so there are no warp hacks and no two-device desync.

Native UGUI (`Button`, `Toggle`, `ScrollRect`, drag, hover, `IPointerXxx`) works without per-widget code: the package presents `InputSystemUIInputModule` a virtual pointer device (`VirtualUiPointer`) driven by `ScreenPosition`/actions.

Cursor appearance is a **render-agnostic skin system**: swappable theme sets (by key at runtime), resolution scaling (global tiers), sprite by action state, with upward fallback. Rendering goes through `ICursorRenderer` (default: a UGUI `Image` at the cursor position; optional: the OS cursor via `Cursor.SetCursor`).

**Input is a pluggable module.** Sources are implemented as drivers (`InputDriver`) listed in the `InputDriverSet` config asset and connected at startup by the loader `CursorInputLoader`. The input module is switched on via a toggle in `SdkSettings` (`USING_VORTEX_CURSOR`). The cursor is a **supra-system entity**: there is no situational input gate — a connected driver is always active.

**Contrast with `CursorSystem`:** `CursorSystem` — OS cursor + UGUI hover, mouse-only, the simplified alternative. `VirtualCursorSystem` — virtual cursor + source arbitration + render-agnostic swappable skins + a pluggable driver-based input layer.

Out of scope:
- Gameplay click triggers/mechanics — the consumer's level (`AdvancedButton`/game code); the package exposes position/actions/projection.
- Persistence of the selected theme — the project layer (L2 does not depend on L3 GameCore; see "Theme selection").
- Interpreting the world hit — the package returns a raw `RaycastHit`.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Unity.InputSystem` | `InputAction`, `Mouse`/`MouseState`, `InputState`, custom device |
| `UnityEngine.UI` | `Image`/`Canvas` (UI render), `IPointerEnter/Exit`, `EventSystem` (IsOverUI/hover) |
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

Input layer (pluggable SDK module, #if USING_VORTEX_CURSOR):
  [InputDriverSet] (SO, ICoreAsset)  → [SerializeReference] InputDriver[]      (Resources/Settings)
  [CursorInputLoader] (IProcess)     → Register in Loader · Resources.Load + failfast
                                        · connect per platform · tick via Accumulate (+anti-spam)
  [InputDriver] (POCO, abstract): Connect/Disconnect · NeedsTick/Tick · HidesCursor · SupportsPlatform
     ├─ MouseInputDriver (Analog)     · TouchInputDriver (Point, HidesCursor=true)
     └─ DirectInputDriver (Direct, NeedsTick) · ActionInputDriver (buttons→mask)

Scene MonoBehaviours (not input drivers):
  CursorHoverZone (UGUI→HoverKey) · CameraProvider (projection camera, LIFO)

UGUI bridge:  VirtualUiPointer (: Mouse, separate layout)  ← UiPointerFeeder (LateUpdate: model→device)
Render:       ICursorRenderer → UiImageCursorRenderer (default) | OsCursorRenderer (opt.)
```

### Data flow

```
Source (mouse/stick/touch/keys)
   → InputDriver (resolves the action by id via InputController) → VirtualCursorController.ReportPointer/SetAction
        → PointerModel (ScreenPosition/Actions/HoverKey; hidesCursor by source)
             ├→ CursorSkinResolver → Visual → ICursorRenderer (draws the cursor)
             ├→ UiPointerFeeder → VirtualUiPointer → InputSystemUIInputModule → native UGUI
             └→ Projection (on demand) → RaycastHit
```

---

## Key concepts

### Input drivers as a pluggable module
- `InputDriver` — an abstract **POCO** (not a MonoBehaviour): `Connect()`/`Disconnect()`, `NeedsTick`/`Tick(dt)`, `HidesCursor`, `SupportsPlatform(platform)`. Actions are resolved by string id "Map/Action" via `InputController` (a `[ValueSelector]` dropdown in the inspector).
- `InputDriverSet` — an SO list of drivers (`[SerializeReference]`), `ICoreAsset` → auto-created at `Resources/Settings/InputDriverSet.asset`.
- `CursorInputLoader` — an `IProcess`: registers in `Loader`, in `RunAsync` loads the set from `Resources`, connects drivers for the current platform, starts the per-frame tick. **Failfast**: the module is on (`USING_VORTEX_CURSOR`) but the asset is missing or the list is empty → exception (no silent no-op).
- Switched on via the `cursorInputSdk` toggle in `SdkSettings` (define `USING_VORTEX_CURSOR`). The cursor core (controller/render/skins/UGUI bridge) always compiles; it is the **input layer** that is pluggable.
- **No input gate** — the cursor is supra-system: a connected driver is always active (no situational cut-off).

### Source arbitration (last-source-wins)
`ReportPointer(pos, source)` makes the reporting source active (last-source-wins). `PointerSourceKind`: `Analog` (mouse), `Point` (touch), `Direct` (gamepad/keys — velocity×dt integration, clamped to screen). The mouse jitter threshold from the old implementation is not carried into the new drivers (arbitration is pure last-source-wins).

### Hide cursor by source
A driver declares `HidesCursor` (`TouchInputDriver` = true: touch is a direct contact, no cursor needed). The flag is passed through `ReportPointer(pos, source, hidesCursor)` and set on the controller by last-source-wins; `Recompute` mixes it over the resolver (`Hide = resolved.Hide || pointerHidden`). Switching sources returns the cursor correctly (mouse → visible again).

### Driver tick (TimeController.Accumulate + anti-spam)
Drivers with `NeedsTick` (Direct) are ticked by a self-rescheduling loop via `TimeController.Accumulate` (no hidden runner). The loop is wrapped in `try/catch/finally`: the inner `catch` isolates a failing driver, `finally` guarantees continuation. Anti-spam: a driver exception is logged only on the **first** in a streak; the counter resets on the first clean frame. `Tick` runs on `unscaledDeltaTime` — it works during pause (menus) too.

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

### Virtual UI pointer and native UGUI
`VirtualUiPointer` — a `Mouse` subclass with a **separate layout** (`<VirtualUiPointer>`): `InputSystemUIInputModule` binds to it and generates all native events. `UiPointerFeeder` writes the device from the model in `LateUpdate`: `position ← ScreenPosition`, buttons ← mask bits (Action1→left…Action5→forward), scroll ← Action6/7. The real mouse feeds a driver (Analog); the UI module reads only the virtual pointer.

### IsOverUI
`IsOverUiHandler` writes `PointerModel.IsOverUI` from `EventSystem.IsPointerOverGameObject()` — world-projection consumers gate the click on this flag.

### Screen→world projection
`VirtualCursorController` keeps a LIFO camera registry (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — a lazy `Physics.Raycast` cached per frame. The package returns the raw hit.

### Theme selection (save-agnostic)
`CursorSkinSelector` holds the reactive theme key (`Selected`) and `Select(key)`. Persistence (`IGameData`) is at the **project layer**. The package (L2) does not depend on GameCore (L3).

---

## Contract

### Input
- `SdkSettings`: the `cursorInputSdk` toggle is on (define `USING_VORTEX_CURSOR`).
- `InputDriverSet` (SO in `Resources/Settings`): a non-empty list of drivers with assigned action ids.
- Input Actions: actions for the drivers (mouse position, touch position, move vector, buttons Action1…Action10); the module's UI map bound to `<VirtualUiPointer>`.
- `CursorSkinSettings` (SO) passed to `Init` (via `VirtualCursorBootstrap`).

### Output
- `PointerModel` (position/source/mask/hover/over-UI) — reactive.
- `CursorVisual` — the current cursor look (sprite+hotspot+hide) for renderers.
- A virtual device driving native UGUI.
- `RaycastHit`/projection point on demand.

### Guarantees
- One screen position for render, UI and projection — no desync.
- Simultaneous actions on the device; a single dominant for the sprite.
- `canceled`/alt-tab clears action bits — no stuck buttons.
- The driver tick survives one driver's failure (try/catch/finally, log without spam).
- Ownership of reactive fields is bound to the controller — not writable from outside.

### Limitations
- The input layer requires the `USING_VORTEX_CURSOR` define; otherwise the drivers don't compile and nothing feeds the position.
- `InputDriverSet` must exist and be non-empty — otherwise `CursorInputLoader` throws (failfast).
- The UGUI module **must** bind to `<VirtualUiPointer>`, otherwise UI won't follow the cursor.
- Projection requires a registered camera; without one — a miss.
- `OsCursorRenderer` requires a **standalone texture** for the sprite (`Cursor.SetCursor` takes a whole `Texture2D`). For atlased cursors use `UiImageCursorRenderer`.
- `InputController` (the input bus) must be available at connect time — it lazily initializes on first access (`GetAction`), so no explicit wait is needed in `WaitingFor`.

---

## API

### VirtualCursorBus (static)
```csharp
static PointerModel     Data;      // runtime model
static CursorVisualData Visual;    // current cursor look
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
// intake (internal): ReportPointer(pos,src) / ReportPointer(pos,src,hidesCursor)
//                    / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### CursorSkinSelector (static)
```csharp
static StringData Selected;                 // reactive theme key
static void Select(string setKey);
static bool IsSelected(string setKey);
```

### InputDriver (abstract, POCO)  [#if USING_VORTEX_CURSOR]
```csharp
abstract void Connect();
abstract void Disconnect();
virtual  bool NeedsTick { get; }            // Direct → true
virtual  void Tick(float unscaledDeltaTime);
virtual  bool HidesCursor { get; }          // Point/Touch → true
virtual  bool SupportsPlatform(RuntimePlatform platform);
// helpers: ResolveAction / EnableMap / DisableMap / SubscribeAction / UnsubscribeAction / Report
```

### InputDriverSet (SO, ICoreAsset) / CursorInputLoader (IProcess)  [#if USING_VORTEX_CURSOR]
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

### 3. Skin config
`Create → Vortex/UI/Cursor Skin Settings`. Fill `resolutionTiers` (ascending), `defaultSetKey`, `sets` — themes; in each theme — packs per tier, base/hover skins, `defaultSprite` + sparse `overrides` (action→sprite).

### 4. Input Actions
Actions for the drivers (mouse position, move, touch, buttons Action1…Action10). In the module's UI map rebind `Point → <VirtualUiPointer>/position`, `Left Click → .../leftButton`, `Right/Middle/Forward/Back`, `ScrollWheel → .../scroll`.

### 5. Scene
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, projection params) — on a persistent object.
- `UiPointerFeeder` — there too. **Input drivers are not placed on the scene** — they live in the `InputDriverSet`.
- An overlay `Canvas` (Screen Space - Overlay, above all UI) + a cursor `Image` (Raycast Target off) + `UiImageCursorRenderer`.
- Optional: `IsOverUiHandler`, `CameraProvider` (on the camera), `CursorHoverZone` (on interactive UGUI elements, hover-skin key).

### 6. Theme persistence (project layer)
`CursorSkinData : IGameData` + a mirror: on load/new-game `CursorSkinSelector.Select(data.SelectedSetKey)`, on `Selected.OnUpdate` write it back.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Module off (`USING_VORTEX_CURSOR` off) | Drivers don't compile; nothing feeds the position |
| `InputDriverSet` missing / empty | `CursorInputLoader` throws (failfast on load) |
| Driver doesn't support the platform | Skipped at connect (`SupportsPlatform`) |
| Active source is touch (`Point`) | Cursor hidden (`HidesCursor`); mouse/gamepad show it again |
| Exception in a driver's `Tick` | Logged only on the first in a streak; the loop lives, other drivers tick |
| `CursorSkinSettings` not passed to `Init` | `Visual` = None; cursor not drawn |
| Theme key not found | Default (`defaultSetKey`), else the first |
| Resolution above all tiers | Largest tier; below all — the smallest |
| Action without a sprite in a skin | Fallback up: base skin → its `defaultSprite`; nowhere → None |
| Skin with `HideCursor` | Cursor hidden, no sprite applied |
| UI module not bound to `<VirtualUiPointer>` | Native UGUI doesn't follow the virtual cursor |
| Alt-tab with a button held | `canceled` clears the bit — no stuck state |
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
│   ├── VirtualUiPointer.cs  UiPointerFeeder.cs  IsOverUiHandler.cs
├── InputDrivers/                         # #if USING_VORTEX_CURSOR — pluggable input layer
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  TouchInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs
├── Drivers/                              # MonoBehaviour, scene-bound (not input drivers)
│   ├── CursorHoverZone.cs  CameraProvider.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
├── DefineSettings/                       # SDK toggle (folded into the SdkSettings assembly via .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
└── ru.vortex.unity.virtualcursorsystem.asmdef
```

Theme persistence (`CursorSkinData : IGameData` + mirror) lives at the project layer, outside the package.
