# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Assembly:** `ru.vortex.unity.cursorsystem`

---

## Purpose

Custom cursor for UGUI projects. Each cursor state is described by a **set** (`CursorHoverEntry`) of three sprites: `Default` (no press), `Action` (LMB), `AltAction` (RMB), plus a `HideCursor` flag (hide the system cursor under a custom overlay). One such set is the base one (outside hover zones); the rest are hover variants addressed by a string **key** (`CursorHoverEntry.Name`) from UI zones.

Sets are grouped into **packs by resolution range** — the controller picks the pack matching the current `Screen.height` (one set of sprites for 1080p, another for 4K). A hover key missing in the selected pack **is inherited from an earlier one** (see cross-pack fallback). Application to the system cursor goes through `Cursor.SetCursor` in `ForceSoftware` mode; mouse events through the Unity Input System (no polling).

Optionally, `GamepadCursorDriver` lets a **gamepad** drive this same system cursor — the left stick moves the real OS mouse directly (which the controller themes), coexisting with the physical mouse.

Out of scope:
- Gestures, drag logic, click feedback in game mechanics — that is the `AdvancedButton` / `InputBusSystem` level.
- World-space cursor (a scene object) — a different pattern; here only the Unity system cursor.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.AppSystem` | `App.OnExit` — proper InputAction disposal |
| `Vortex.Core.SettingsSystem` | `Settings.OnInit`, partial `SettingsModel` extension |
| `Vortex.Core.Extensions.ReactiveValues` | `BoolData`, `StringData` with owner-protected writes |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — config base class |
| `Vortex.Unity.Extensions.Editor` | `MenuConfigSearchController` — config-locator menu command (editor) |
| `Unity.InputSystem` | `InputAction` for LMB/RMB and `GamepadCursorDriver` bindings; `Mouse`/`InputState` there |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` in `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]`, `[FoldoutGroup]` |

`SettingsModelExt/ru.vortex.settings.asmref` injects the partial settings-model extension into the `ru.vortex.settings` assembly so cursor fields live in the shared `SettingsModel`. The `CursorPack`, `CursorResolutionPack`, and `CursorHoverEntry` types also live in `SettingsModelExt/` and compile into the settings assembly — a back-reference from it to the cursor package is impossible (cycle), so the data models are placed where both the settings assembly and the package itself can see them.

---

## Architecture

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorPacks: CursorResolutionPack[]   (ascending maxScreenHeight)
          ├── { maxScreenHeight, CursorPack }   ← pack for resolutions ≤ maxScreenHeight
          └── ...
                  │  CursorPack = { CursorDefault: CursorHoverEntry,      ← base set
                  │                 CursorOnHover: CursorHoverEntry[] }    ← hover variants by key
                  │
                  │  CursorHoverEntry = { Name, Default, Action(LMB),
                  │                       AltAction(RMB), HideCursor }
                  │  (via Settings.OnInit + partial SettingsModel)
                  ▼
[Settings.Data() in SettingsModel]
   └── CursorPacks: CursorResolutionPack[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — reads packs, SelectPack by Screen.height, raises InputActions
   ├── SelectPack(packs) — picks the pack for the current resolution (+ remembers index for fallback)
   ├── RefreshResolution() — public re-pick of the pack after a resolution change
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(key) / OnUnHover(key) ← public API from the view layer
   └── ApplyByPriority() — pick the set (hover key / base) → the set hides the cursor or
                           ResolveHover(set) → Cursor.SetCursor(ForceSoftware)

[MouseHoverListener] (MonoBehaviour, on UGUI objects)
   └── IPointerEnter/Exit → CursorController.OnHover(key) / OnUnHover(key)

[MouseKeyMap] (POCO, exposed via CursorController.MouseKeys)
   ├── BoolData   LeftKeyPressed
   ├── BoolData   RightKeyPressed
   └── StringData HoverKey     (empty/null = no hover)

[GamepadCursorDriver] (MonoBehaviour, optional)
   └── drives the REAL system Mouse directly from bound input actions (move → position, buttons → LMB/RMB):
       one device → CursorController, the UI module and clicks all read the same mouse (no desync)
```

### Pack selection by resolution

`SelectPack(CursorResolutionPack[])` (`CursorController.cs`) picks the pack as follows:

1. Among packs with `MaxScreenHeight >= Screen.height`, the **smallest** matching threshold is taken — the "tightest" pack covering the current resolution.
2. If the current resolution is above all thresholds, the **largest** pack (highest `MaxScreenHeight`) is taken.

Example: packs with thresholds `1080`, `1440`, `2160`. At `Screen.height == 1440` → pack `1440`. At `Screen.height == 3000` (above all) → pack `2160`.

Besides the selected pack, the controller remembers its **index** — needed for the cross-pack hover-key fallback.

### Cross-pack hover-key fallback

Packs **must be ordered by ascending `MaxScreenHeight`** (first = low resolution). A hover key (`CursorHoverEntry.Name`) is resolved by `ResolveHoverEntry`: starting from the selected pack and going **down the array toward the first**. A key missing in the selected ("higher") pack **is inherited from an earlier one**; there is no upward fallback. A key found nowhere → the base set.

This replaces the former "equal length and order of hover arrays across all sets" requirement: now each pack may have its own set of keys, and missing ones are picked up from a lower-resolution pack. `CursorSettings.OnValidate` logs a `LogWarning` if packs are not in ascending `MaxScreenHeight` order (which the fallback depends on).

### Re-pick after a resolution change

```csharp
CursorController.RefreshResolution();
```

Public method. Call it after applying video settings (resolution / window mode change) — the controller re-picks the pack for the new `Screen.height` and applies the cursor for the current state. Button and hover state are preserved. No-op if the cursor is hardware or the controller isn't initialized.

### Cursor application

`ApplyByPriority` (`CursorController.cs`) works in two steps:

1. **Set selection.** If a hover key is active (`HoverKey` non-empty) and it resolves across packs (`ResolveHoverEntry`) → its set. Otherwise (outside a zone, or the key is found nowhere) → the pack's base set (`CursorDefault`).
2. **Within the set** (`ResolveHover`): LMB pressed → `Action`, RMB pressed → `AltAction`, otherwise → `Default`. An unset action field falls back to the set's `Default`; an empty `Default` on a hover variant → the base set's `Default` (via `Apply(null)`).

If the selected set has `HideCursor` set, the system cursor is hidden (`Cursor.visible = false`, idempotently via `SetCursorHidden`) and no sprite is applied: such a set is meant for a custom cursor overlay.

So LMB/RMB presses now act **within the active set** (a hover zone's or the base one), not as separate global cursors.

### Hardware cursor

If the pack list in `CursorSettings` is empty (or `null`), `Init()` returns early, InputActions are not created, `Cursor.SetCursor` is not called — the cursor stays the system (hardware) one. This lets you disable the custom cursor globally by clearing the list, without code changes.

### ForceSoftware mode

`Apply(Sprite)` calls `Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware)`. The OS hardware cursor is limited in size and format (on most platforms — 32×32, a fixed texture format), and `CursorMode.Auto` hands the sprite to the hardware, cropping/scaling it to those limits. `ForceSoftware` makes the engine draw the cursor itself — the sprite renders "as authored", any size and quality. The cost is that the cursor is drawn one frame later than the hardware one, which is visually unnoticeable for a custom cursor.

### Alt-tab protection

Subscriptions to `InputAction.started/canceled` through the Unity Input System — on window focus loss InputSystem soft-resets the device and sends `canceled` to all active actions. `MouseKeys.LeftKeyPressed` / `RightKeyPressed` return to `false` automatically, and `ApplyByPriority` reverts the cursor to the set's `Default`. An action cursor cannot get stuck after alt-tab.

### Hover-zone race protection

In `OnUnHover(key)` the check `MouseKeys.HoverKey.Value != key`: if the key is no longer ours (another zone captured hover in the same frame), the call is ignored. This scenario is typical for nested hover zones in UGUI — EventSystem sends the inner zone's `OnPointerEnter` before the parent's `OnPointerExit`.

### External-write protection for `MouseKeys`

`Run()` (under `[RuntimeInitializeOnLoadMethod]`) calls `SetOwner(Key)` on `LeftKeyPressed`, `RightKeyPressed`, and `HoverKey`. After that a value cannot be written from outside — `Set(value, ownerKey)` refuses if `ownerKey` doesn't match. Only reading and subscribing are available externally.

### Hotspot

In `Apply(Sprite)` the cursor hotspot is taken from `Sprite.pivot` and inverted along Y: a Unity Sprite uses a bottom-left coordinate system while `Cursor.SetCursor` expects top-left. The designer sets the sprite pivot the usual way in the importer; the controller does the inversion.

### Gamepad cursor (GamepadCursorDriver)

`GamepadCursorDriver` (`GamepadCursorDriver.cs`) is an **optional** `MonoBehaviour` that lets a gamepad drive the same system cursor the controller themes — cooperatively with the mouse, with no second cursor.

It drives the **real** system mouse (`Mouse.current`) directly: the `moveAction` vector adds an offset to the current mouse position (`InputState.Change` + `Mouse.WarpCursorPosition`), and `leftButtonAction` / `rightButtonAction` are injected as the mouse's left / right button. Because it is **one device**, `CursorController` (which draws at the OS-cursor position), the UI module (`<Mouse>/position`) and clicks (`<Mouse>/leftButton`) all read the same mouse — no desync. Mouse and stick coexist: the stick adds to the current position, and when idle the driver touches nothing, so the physical mouse works natively.

**Why not `VirtualMouseInput`:** VMI creates a **separate** virtual `Mouse` device; combined with CursorSystem (which renders on the **real** OS cursor) that desyncs — the stick moves the virtual mouse (which the UI reads) while the visible cursor stays put, so clicks land off. Driving the real mouse directly avoids it.

**Bindings** (`InputActionProperty`, set in the inspector — inline action or asset reference): `moveAction` (Value/Vector2 — e.g. the left stick), `leftButtonAction` / `rightButtonAction` (Button — e.g. South / East). **Fields:** `speed` (px/sec), `deadzone`. Buttons are re-asserted each frame while held (so a physical mouse event can't drop the injected press) and cleared once on release (and on disable). Runs on `unscaledDeltaTime`, so it works while the game is paused (menus). Requires a mouse device (desktop) — on a system without a mouse there is no OS cursor for CursorSystem to theme.

---

## Usage

### 1. Create a settings preset

`Assets → Create → Vortex → CursorSettings` (the exact menu depends on how the `SettingsPreset` pipeline is registered in your project). Quick access to an existing config — the **Tools → Vortex → Configs → Cursor Settings** menu (pings the asset in Project).

Fill `cursorPacks` — an array of packs by resolution, **in ascending `maxScreenHeight`**. In each pack:
- `maxScreenHeight` — the upper bound of vertical resolution for this pack.
- `CursorDefault` — the base set (`CursorHoverEntry`): `Default` is required to activate the system; `Action`/`AltAction` are optional (click feedback); `HideCursor` hides the system cursor.
- `CursorOnHover[]` — hover variants. Each has a `Name` (key) and its own `Default`/`Action`/`AltAction`/`HideCursor`.

> ⚠️ **Packs must be ordered by ascending `maxScreenHeight`** — hover-key inheritance from an earlier pack depends on it. `OnValidate` warns about order violations. Hover-array lengths no longer have to match.

The minimal configuration is a single pack with a large `maxScreenHeight` (e.g. `99999`): it applies at any resolution.

### 2. Attach `MouseHoverListener` to a UGUI element

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (key picked from a dropdown in the inspector)
```

The dropdown pulls the **merged list of unique keys** (`CursorHoverEntry.Name`) across all packs of the active `CursorSettings` — each pack may have its own set. The designer picks by name. The "[NONE]" item = an empty key disables hover switching for the zone.

### 3. Re-pick the cursor after a resolution change

```csharp
// In the video-settings apply handler:
VideoController.ApplyResolution(newResolution);
CursorController.RefreshResolution();   // picks up the pack for the new Screen.height
```

### 4. Programmatic hover

If a hover zone is not UGUI (world-space collider, custom raycast logic, hotkey emulation) — call directly by key:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private string cursorKey = "interact";

    private void OnMouseEnter() => CursorController.OnHover(cursorKey);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorKey);
}
```

### 5. Subscribing to mouse state externally

```csharp
private void OnEnable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate += OnLmbChanged;
    CursorController.MouseKeys.HoverKey.OnUpdate += OnHoverChanged;
}

private void OnDisable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate -= OnLmbChanged;
    CursorController.MouseKeys.HoverKey.OnUpdate -= OnHoverChanged;
}

private void OnLmbChanged(bool pressed) { ... }
private void OnHoverChanged(string key) { ... }
```

A writeback via `MouseKeys.LeftKeyPressed.Set(true, ?)` from outside won't pass — the owner is bound to the controller.

### 6. Gamepad cursor via stick (optional)

1. Add **`GamepadCursorDriver`** to any always-active object (e.g. the one that owns the `EventSystem`).
2. Assign the bindings: `moveAction` → `<Gamepad>/leftStick` (Value/Vector2), `leftButtonAction` → `<Gamepad>/buttonSouth`, `rightButtonAction` → `<Gamepad>/buttonEast`. Tune `speed` / `deadzone` if needed.
3. Done — no `VirtualMouseInput` required: the mouse works as before (the controller themes the OS cursor); the bound move axis moves the same cursor, the button actions click (right → AltAction); over a `HideCursor` zone the cursor hides as usual while the position keeps tracking.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `cursorPacks` list empty or `null` | Controller doesn't activate, cursor is the system one |
| Selected pack without `CursorDefault.Default` | `Apply` throws on `_defaultSet.Default.texture` (fail-fast: the base set must have a `Default`) |
| `Screen.height` above all thresholds | The largest pack (highest `maxScreenHeight`) is taken |
| `Screen.height` below all thresholds | The pack with the smallest threshold is taken |
| Packs not in ascending `maxScreenHeight` order | `OnValidate` → `LogWarning`; cross-pack key fallback will be incorrect |
| Hover key present in an earlier pack but not the selected one | Inherited from the earlier one (down toward the first) |
| Hover key found in no pack | Falls back to the base set |
| Set's `Action`/`AltAction` == null on press | Falls back to the set's `Default` |
| Hover variant's `Default` == null | Falls back to the base set's `Default` (`Apply(null)`) |
| Active set has `HideCursor == true` | The system cursor is hidden, no sprite applied |
| `RefreshResolution()` with hardware cursor / before Init | No-op |
| Alt-tab / focus loss with a button pressed | InputSystem sends `canceled` → state resets → cursor reverts to `Default` |
| Nested hover zones (A contains B) | Enter(B) sets key B; a late Exit(A) is ignored (race) |
| Re-initialization (restart without domain reload) | Old InputActions are disposed before raising new ones |
| `App.OnExit` | `DisposeActions` disposes InputActions normally |
| Opening a scene without an active `EventSystem` | `MouseHoverListener` gets no Enter/Exit — the cursor works only via the base set's presses |
| No gamepad / no mouse device | `GamepadCursorDriver` no-ops that frame (needs both `Gamepad.current` and `Mouse.current`) |
| Physical mouse moved while the stick is pushed | Both add to the same real mouse — cooperative, no mode switch |
| Physical left-click held together with gamepad `South` | On `South` release the injected left button is cleared once — a rare simultaneous physical hold may be dropped |
| Game paused (`timeScale == 0`) | Still works — the driver uses `unscaledDeltaTime` |

The fail-fast policy on a missing base-set `Default` is intentional: an invalid configuration should crash early and loud so the designer sees the problem during development rather than silently getting the "wrong cursor" in production. See `architecture_context.md` on fail-fast in the core.

---

## File Structure

```
CursorSystem/
├── CursorController.cs                       # static bus, pack selection, InputSystem subscriptions, set/sprite resolution
├── CursorSettings.cs                         # SettingsPreset (SO) with a CursorResolutionPack array + OnValidate
├── MouseHoverListener.cs                     # MonoBehaviour for UGUI zones (hover key via ValueDropdown)
├── MouseKeyMap.cs                            # POCO model: BoolData/StringData with owner protection
├── GamepadCursorDriver.cs                    # MonoBehaviour: drives the real mouse from a gamepad (optional)
├── Editor/
│   └── MenuController.cs                     # Tools/Vortex/Configs/Cursor Settings — config locator
├── SettingsModelExt/
│   ├── CursorHoverEntry.cs                   # a Default/Action/AltAction set + Name + HideCursor
│   ├── CursorPack.cs                         # base set + hover-variant array (in the settings assembly)
│   ├── CursorResolutionPack.cs               # CursorPack + maxScreenHeight threshold (in the settings assembly)
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel with a CursorPacks field
│   └── ru.vortex.settings.asmref             # type injection + partial into the settings assembly
└── ru.vortex.unity.cursorsystem.asmdef
```
