# CursorSystemAdvanced

**Namespace:** `Vortex.Unity.UI.CursorSystemAdvanced`
**Assembly:** `ru.vortex.unity.cursorsystemadvanced`

---

## Purpose

A multi-source virtual cursor for UGUI projects. A single screen position (`ScreenPosition`) is fed by any source wired in Input Actions — mouse, gamepad, keys, touch — and is the single source of truth. The position is **decoupled from the OS mouse**: it lives in the model, not in the Mouse device, so there are no warp hacks and no two-device desync.

Native UGUI (`Button`, `Toggle`, `ScrollRect`, drag, hover, `IPointerXxx`) works without per-widget code: the package presents `InputSystemUIInputModule` a virtual pointer device (`VirtualUiPointer`) driven by `ScreenPosition`/actions.

Cursor appearance is a **render-agnostic skin system**: swappable theme sets (selected at runtime by key), resolution scaling (global tiers), sprite by action state, with upward fallback through the settings. Rendering goes through `ICursorRenderer` (default: a UGUI `Image` at the cursor position; optional: the OS cursor via `Cursor.SetCursor`).

**Contrast with `CursorSystem`:** `CursorSystem` — OS cursor + UGUI hover, mouse-only, the simplified alternative. `CursorSystemAdvanced` — virtual cursor + source arbitration + render-agnostic swappable skins.

Out of scope:
- Gameplay click triggers/mechanics — the consumer's level (`AdvancedButton`/game code); the package exposes position/actions/projection.
- Persistence of the selected theme — the project layer (L2 does not depend on L3 GameCore; see "Theme selection").
- Interpreting the world hit — the package returns a raw `RaycastHit`.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Unity.InputSystem` | `InputAction`/`InputActionProperty`, `Mouse`/`MouseState`, `InputState`, custom device |
| `UnityEngine.UI` | `Image`/`Canvas` (UI render), `IPointerEnter/Exit`, `EventSystem` (IsOverUI/hover) |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `EnumData`/`StringData`/`BoolData` with owner protection |
| `Vortex.Unity.Extensions.ReactiveValues` | `Vector2Data` |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ClassLabel]` |
| Sirenix Odin Inspector | `[Tooltip]`/inspector attributes |

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
 ├─ ReportPointer / SetAction / SetHover / SetOverUI      (internal — drivers)
 ├─ Recompute → CursorSkinResolver → Visual (CursorVisualData)
 └─ Projection: RegisterCamera(LIFO) + lazy raycast (TryGetWorldHit/GetWorldProjection)

[VirtualCursorBus] (static)  → Data / Visual / IsReady / OnReady            (read-only facade)
[CursorSkinSelector] (static) → Selected(StringData) / Select(key)          (save-agnostic)

Drivers (sources, in-package):
  MousePointerDriver(Analog+threshold) · DirectPointerDriver(Direct) · TouchPointerDriver(Point)
  PointerActionDriver(buttons→mask) · CursorHoverZone(UGUI→HoverKey) · CameraProvider(LIFO)

UGUI bridge:  VirtualUiPointer (: Mouse, separate layout)  ← UiPointerFeeder (LateUpdate: model→device)
Render:       ICursorRenderer → UiImageCursorRenderer (default) | OsCursorRenderer (opt.)
```

### Data flow

```
Source (mouse/stick/touch/keys) → Driver → VirtualCursorController.ReportPointer/SetAction
   → PointerModel (ScreenPosition/Actions/HoverKey)
        ├→ CursorSkinResolver → Visual → ICursorRenderer (draws the cursor)
        ├→ UiPointerFeeder → VirtualUiPointer → InputSystemUIInputModule → native UGUI
        └→ Projection (on demand) → RaycastHit
```

---

## Key concepts

### Source arbitration (last-source-wins + threshold)
`ReportPointer(pos, source)` makes the reporting source active. The activation threshold lives in the driver (source specifics): while a source is inactive, it captures control only on a notable move (mouse jitter doesn't steal the cursor from the gamepad); an active source tracks every move without a threshold. `PointerSourceKind`: `Analog` (mouse), `Point` (touch), `Direct` (gamepad/keys — velocity×dt integration, clamped to screen).

### Action mask (simultaneity + dominant)
`PointerAction` — a sequential index enum (`None` + `Action1…Action10`, comments fix the convention: 1=LMB, 2=RMB, 3=MMB, 4=Back, 5=Forward, 6=Scroll↑, 7=Scroll↓, 8–10=reserve). `PointerActionMask` — a `readonly struct` over `int`: bits = simultaneously active actions (for the device); `Dominant()` — the lowest active bit by priority (for sprite selection). The `PointerActionDriver` sets/clears bits on `started`/`canceled`; `canceled` on alt-tab clears them by itself.

### Skins: theme → tier → hover → action, with upward fallback
`CursorSkinResolver.Resolve`:
1. **Theme** — `CursorSkinSelector.Selected` → `CursorSkinSet` (default if not found).
2. **Resolution tier** — `SelectTierIndex(Screen.height)`: smallest `resolutionTiers[i] >= height`, else the largest → `CursorSkinPack`.
3. **Skin** — hover skin by `HoverKey`, else the base; `HideCursor` → cursor hidden, no sprite applied.
4. **Sprite** — by `Actions.Dominant()`: skin `override` → its `defaultSprite` → **up**: the pack's base skin → its `defaultSprite`. No clamp — an unset action climbs the chain to the nearest configured one or the default.
5. Hotspot — from `Sprite.pivot`, Y-inverted (`pivot` bottom-left → hotspot top-left).

### Global resolution tiers
Breakpoints (`resolutionTiers`) are defined **once** in `CursorSkinSettings` — a single device concern; each theme provides one pack per tier (index-aligned, `OnValidate` warns on mismatch). On a resolution change → `VirtualCursorController.RefreshResolution()`.

### Virtual UI pointer and native UGUI
`VirtualUiPointer` — a `Mouse` subclass with a **separate layout** (`<VirtualUiPointer>`): `InputSystemUIInputModule` binds to it and generates all native events (click/hover/drag/scroll) for mouse and gamepad uniformly. `UiPointerFeeder` writes the device from the model in `LateUpdate`: `position ← ScreenPosition`, buttons ← mask bits (Action1→left…Action5→forward), scroll ← Action6/7. `MousePointerDriver` ignores events from its own `VirtualUiPointer` (otherwise a feedback loop). The real mouse feeds the Analog driver (`<Mouse>`); the UI module reads only the virtual pointer.

### IsOverUI
`IsOverUiHandler` writes `PointerModel.IsOverUI` from `EventSystem.IsPointerOverGameObject()` — world-projection consumers gate the click on this flag (a replacement for UITK picking).

### Screen→world projection
`VirtualCursorController` keeps a LIFO camera registry (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — a lazy `Physics.Raycast` at the cursor position, cached per frame (`InvalidateProjection` resets). The package returns the raw hit.

### Theme selection (save-agnostic)
`CursorSkinSelector` holds the reactive theme key (`Selected`) and `Select(key)`. Persistence (`IGameData`) is at the **project layer**: `CursorSkinData` + a mirror that reads the key on load/new-game and writes it on `Selected` changes. The package (L2) does not depend on GameCore (L3).

---

## Contract

### Input
- Input Actions: position (`<Mouse>/position`), move vector (`<Gamepad>/leftStick`), touch position, buttons for `Action1…Action10`.
- UI module bound to `<VirtualUiPointer>` (Point/Click/Scroll).
- `CursorSkinSettings` (SO) passed to `Init`.

### Output
- `PointerModel` (position/source/mask/hover/over-UI) — reactive.
- `CursorVisual` — the current cursor look (sprite+hotspot+hide) for renderers.
- A virtual device driving native UGUI.
- `RaycastHit`/projection point on demand.

### Guarantees
- One screen position for render, UI and projection — no desync.
- Simultaneous actions on the device; a single dominant for the sprite.
- `canceled`/alt-tab clears action bits — no stuck buttons.
- Ownership of reactive fields is bound to the controller — not writable from outside.

### Limitations
- The UGUI module **must** bind to `<VirtualUiPointer>`, otherwise UI won't follow the virtual cursor.
- `IsOverUI` uses the no-arg `IsPointerOverGameObject()` — with several active pointers a pointerId may be needed.
- Projection requires a registered camera; without one — a miss.
- A mouse device is required for the mouse-based UI module (desktop); a pure console is out of scope.
- `OsCursorRenderer` requires a **standalone texture** for the sprite: `Cursor.SetCursor` takes a whole `Texture2D` and would draw the entire atlas. For atlased cursors use `UiImageCursorRenderer` (hotspot is computed from the sprite rect, atlas supported).

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
static void RefreshResolution();                       // after a resolution / window-mode change
static void ConfigureProjection(LayerMask mask, float distance);
static bool TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void InvalidateProjection();
// intake (internal): ReportPointer / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### CursorSkinSelector (static)
```csharp
static StringData Selected;                 // reactive theme key
static void Select(string setKey);
static bool IsSelected(string setKey);
```

---

## Usage

### 1. Skin config
`Create → Vortex/UI/Cursor Skin Settings`. Fill `resolutionTiers` (ascending), `defaultSetKey`, `sets` — themes; in each theme — packs per tier, base/hover skins, `defaultSprite` + sparse `overrides` (action→sprite).

### 2. Input Actions
Actions for the drivers: position (`<Mouse>/position`), move (`<Gamepad>/leftStick`), touch, buttons Action1…Action10 (mouse/gamepad/scroll). In the module's UI map rebind `Point → <VirtualUiPointer>/position`, `Left Click → .../leftButton`, `Right/Middle/Forward/Back`, `ScrollWheel → .../scroll`.

### 3. Scene
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, projection params) — on a persistent object.
- `UiPointerFeeder`, drivers (`MousePointerDriver`/`DirectPointerDriver`/`TouchPointerDriver`/`PointerActionDriver` with bindings) — there too.
- An overlay `Canvas` (Screen Space - Overlay, sort order above all UI) + a cursor `Image` (Raycast Target off) + `UiImageCursorRenderer` (references to the RectTransform/Image).
- Optional: `IsOverUiHandler`, `CameraProvider` (on the camera), `CursorHoverZone` (on interactive UGUI elements, hover-skin key).

### 4. Theme persistence (project layer)
`CursorSkinData : IGameData` + a mirror: on load/new-game `CursorSkinSelector.Select(data.SelectedSetKey)`, on `Selected.OnUpdate` write it back.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `CursorSkinSettings` not passed to `Init` | `Visual` = None; cursor not drawn |
| Theme key not found | Default (`defaultSetKey`), else the first |
| Resolution above all tiers | Largest tier; below all — the smallest |
| Fewer/more packs than tiers | Clamped to range; `OnValidate` warns |
| Action without a sprite in a skin | Fallback up: base skin → its `defaultSprite`; nowhere → None |
| Skin with `HideCursor` | Cursor hidden, no sprite applied |
| UI module not bound to `<VirtualUiPointer>` | Native UGUI doesn't follow the virtual cursor |
| Alt-tab with a button held | `canceled` clears the bit — no stuck state |
| Projection without a registered camera | Miss (`false`/`null`) |
| Real mouse and Direct at once | Threshold gates the capture; the active source tracks without a threshold |
| Atlased cursor sprite | `UiImageCursorRenderer` — OK (hotspot from the sprite rect); `OsCursorRenderer` — needs a standalone texture |

---

## File Structure

```
CursorSystemAdvanced/
├── Bus/VirtualCursorBus.cs
├── VirtualCursorController.cs            # static core: model, Visual resolve, intake
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
├── Drivers/
│   ├── MousePointerDriver.cs  DirectPointerDriver.cs  TouchPointerDriver.cs
│   ├── PointerActionDriver.cs  CursorHoverZone.cs  CameraProvider.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
└── ru.vortex.unity.cursorsystemadvanced.asmdef
```

Theme persistence (`CursorSkinData : IGameData` + mirror) lives at the project layer, outside the package.
