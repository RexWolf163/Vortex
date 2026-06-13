# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Assembly:** `ru.vortex.unity.cursorsystem`

---

## Purpose

Custom cursor for UGUI projects: a default sprite, separate LMB/RMB sprites, and an array of hover variants switched by UI zones. Cursors are grouped into **resolution packs** — the controller selects the matching pack for the current `Screen.height` (one set of sprites for 1080p, another for 4K). Applying a sprite to the system cursor goes through `Cursor.SetCursor` in `ForceSoftware` mode; mouse events go through Unity Input System (no polling).

Out of scope:
- Gestures, drag logic, click feedback for game mechanics — that's the job of `AdvancedButton` / `InputBusSystem`.
- A world-space cursor (an in-scene object) — that's a different pattern; this package only drives the system cursor.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.AppSystem` | `App.OnExit` — proper release of `InputAction`s |
| `Vortex.Core.SettingsSystem` | `Settings.OnInit`, partial extension of `SettingsModel` |
| `Vortex.Core.Extensions.ReactiveValues` | `BoolData`, `IntData` with owner-protected writes |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — config base class |
| `Unity.InputSystem` | `InputAction` for LMB/RMB |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` in `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]` |

`SettingsModelExt/ru.vortex.settings.asmref` injects the partial extension of the settings model into the `ru.vortex.settings` assembly so cursor fields live in the unified `SettingsModel`. The `CursorPack` and `CursorResolutionPack` types also live in `SettingsModelExt/` and compile into the settings assembly — a back-reference from it to the cursor package is impossible (cycle), so the data models are placed where both the settings assembly and the cursor package can see them.

---

## Architecture

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorPacks: CursorResolutionPack[]
          ├── { maxScreenHeight, CursorPack }   ← pack for resolutions ≤ maxScreenHeight
          ├── { maxScreenHeight, CursorPack }
          └── ...
                  │  CursorPack = { cursorDefault, cursorLeftMouseDown,
                  │                 cursorRightMouseDown, cursorOnHover[] }
                  │
                  │  (via Settings.OnInit + partial SettingsModel)
                  ▼
[Settings.Data() in SettingsModel]
   └── CursorPacks: CursorResolutionPack[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — reads packs, SelectPack by Screen.height, raises InputActions
   ├── SelectPack(packs) — picks the pack for the current resolution
   ├── RefreshResolution() — public re-selection after a resolution change
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(index) / OnUnHover(index) ← public API from the view layer
   └── ApplyByPriority() — LMB > RMB > Hover > Default → Cursor.SetCursor(ForceSoftware)

[MouseHoverListener] (MonoBehaviour, on UGUI objects)
   └── IPointerEnter/Exit → CursorController.OnHover(index) / OnUnHover(index)

[MouseKeyMap] (POCO, exposed via CursorController.MouseKeys)
   ├── BoolData LeftKeyPressed
   ├── BoolData RightKeyPressed
   └── IntData  HoverIndex     (-1 = no hover)
```

### Pack selection by resolution

`SelectPack(CursorResolutionPack[])` (in `CursorController.cs`) picks a pack as follows:

1. Among packs with `MaxScreenHeight >= Screen.height`, the **smallest** matching threshold is taken — the "tightest" pack that still covers the current resolution.
2. If the current resolution exceeds every threshold, the **largest** pack is taken (highest `MaxScreenHeight`).

Example: packs with thresholds `1080`, `1440`, `2160`. At `Screen.height == 1440` → the `1440` pack. At `Screen.height == 3000` (above all) → the `2160` pack.

`cursorOnHover[]` **must have the same length and order across all packs** — the `MouseHoverListener.index` is shared across resolutions. `CursorSettings.OnValidate` logs a warning if hover-array lengths diverge between packs.

### Re-selection after a resolution change

```csharp
CursorController.RefreshResolution();
```

A public method. Call it after applying video settings (resolution / window-mode change) — the controller re-selects the pack for the new `Screen.height` and applies a sprite via the priority cascade. Button and hover state is preserved. It is a no-op if the cursor is hardware or the controller is not initialised.

### Sprite priority

`ApplyByPriority` (in `CursorController.cs`) implements an explicit cascade with `return` statements:

1. **LMB pressed + `cursorLeftMouseDown` non-null** → apply the LMB sprite.
2. **RMB pressed + `cursorRightMouseDown` non-null** → apply the RMB sprite.
3. **`HoverIndex >= 0` + `cursorOnHover[HoverIndex]` non-null** → apply the hover sprite.
4. Otherwise → default sprite.

LMB overrides RMB and hover. RMB overrides hover. Hover is active only when no mouse button is pressed.

### Hardware cursor

If the pack list in `CursorSettings` is empty (or `null`), `Init()` returns early, no `InputAction`s are created, `Cursor.SetCursor` is never called — the cursor stays OS-native. This lets you globally disable the custom cursor by clearing the list, without touching code.

### ForceSoftware mode

`Apply(Sprite)` calls `Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware)`. The OS hardware cursor is limited in size and format (on most platforms 32×32, a fixed texture format), and `CursorMode.Auto` hands the sprite to the hardware, cropping/scaling it to those limits. `ForceSoftware` makes the engine draw the cursor itself — the sprite is shown "as authored", at any size and quality. The cost is that the cursor is drawn one frame later than a hardware one, which is visually unnoticeable for a custom cursor.

### Alt-tab protection

Subscriptions go through `InputAction.started/canceled` over the Unity Input System. On focus loss the Input System performs a soft device reset and dispatches `canceled` to every active action. `MouseKeys.LeftKeyPressed` / `RightKeyPressed` automatically fall back to `false`, and `ApplyByPriority` reverts the cursor to default. An LMB cursor cannot get stuck after alt-tab.

### Hover race protection

`OnUnHover(index)` checks `MouseKeys.HoverIndex != index`: if the active index is no longer ours (another zone has captured hover in the same frame), the call is ignored. The race is typical for nested hover zones in UGUI — the EventSystem dispatches `OnPointerEnter` of the nested zone before `OnPointerExit` of the parent.

### Write protection on `MouseKeys`

`Run()` (annotated with `[RuntimeInitializeOnLoadMethod]`) calls `SetOwner(Key)` on each of the three `BoolData`/`IntData` fields. After that an external write into `MouseKeys.LeftKeyPressed` is rejected — `Set(value, ownerKey)` throws unless `ownerKey` matches. Only reads and subscriptions are available outside.

### Hotspot

In `Apply(Sprite)` the cursor hotspot is taken from `Sprite.pivot` and inverted along Y: Unity Sprite uses bottom-left coordinates, while `Cursor.SetCursor` expects top-left. The designer sets the pivot the usual way in the importer; the inversion is the controller's responsibility.

---

## Usage

### 1. Create a settings preset

`Assets → Create → Vortex → CursorSettings` (the exact menu depends on how the `SettingsPreset` pipeline is wired in your project).

Fill in `cursorPacks` — an array of resolution packs. In each pack:
- `maxScreenHeight` — the upper bound of vertical resolution for this pack.
- `Pack.cursorDefault` — main sprite (required to activate the system).
- `Pack.cursorLeftMouseDown` / `cursorRightMouseDown` — optional, for click feedback.
- `Pack.cursorOnHover[]` — array of sprites for different UI-zone types.

> ⚠️ **The length and order of `cursorOnHover[]` must match across all packs** — `MouseHoverListener.index` addresses the same logical hover across resolutions. `OnValidate` warns on a mismatch.

The minimal configuration is a single pack with a large `maxScreenHeight` (e.g. `99999`): it applies at any resolution.

### 2. Attach `MouseHoverListener` to a UGUI element

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (index is picked from the dropdown in the inspector)
```

The inspector dropdown reads sprite names from the largest pack of the active `CursorSettings` (indices are shared across resolutions) — designers don't memorize numbers, they pick by name. The `[NONE]` entry (`-1`) disables hover switching for that zone. Empty array slots show as `[EMPTY] {i}`.

### 3. Re-select the cursor after a resolution change

```csharp
// In your video-settings apply handler:
VideoController.ApplyResolution(newResolution);
CursorController.RefreshResolution();   // picks the pack for the new Screen.height
```

### 4. Programmatic hover

If the hover zone is not UGUI (a world-space collider, a custom raycast, a hotkey emulator) — call directly:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private int cursorIndex = 0;

    private void OnMouseEnter() => CursorController.OnHover(cursorIndex);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorIndex);
}
```

### 5. Subscribing to mouse state externally

```csharp
private void OnEnable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate += OnLmbChanged;
    CursorController.MouseKeys.HoverIndex.OnUpdate += OnHoverChanged;
}

private void OnDisable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate -= OnLmbChanged;
    CursorController.MouseKeys.HoverIndex.OnUpdate -= OnHoverChanged;
}

private void OnLmbChanged() { ... }
private void OnHoverChanged() { ... }
```

External writeback via `MouseKeys.LeftKeyPressed.Set(true, ?)` won't go through — the owner is bound to the controller.

---

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `cursorPacks` list is empty or `null` | The controller does not activate, the cursor stays system-native |
| Selected pack has no `cursorDefault` | `Apply` throws on `_cursorDefault.texture` (fail-fast: a pack must have a default) |
| `Screen.height` above all thresholds | The largest pack is taken (highest `maxScreenHeight`) |
| `Screen.height` below all thresholds | The pack with the smallest threshold is taken |
| `cursorOnHover[]` lengths diverge between packs | `OnValidate` → `LogWarning`; at runtime the index addresses the "wrong" hover |
| `cursorLeftMouseDown == null` with LMB pressed | Skip the branch, continue down the priority chain (RMB → Hover → Default) |
| `cursorOnHover` empty / index out of range | Any `OnHover(index >= 0)` → `IndexOutOfRangeException` (fail-fast) |
| `cursorOnHover[index] == null` (valid index, missing sprite) | Fall back to default |
| `RefreshResolution()` with a hardware cursor / before Init | No-op |
| Alt-tab / focus loss with a button held | Input System sends `canceled` → state resets → cursor reverts to default |
| Nested hover zones (A contains B) | `Enter(B)` sets the index to B; a late `Exit(A)` is ignored (race) |
| Reinitialisation (restart without domain reload) | Old `InputAction`s are released before the new ones are raised |
| `App.OnExit` | `DisposeActions` releases the `InputAction`s cleanly |
| Scene opened without an active `EventSystem` | `MouseHoverListener` receives no Enter/Exit — the cursor only reacts to LMB/RMB and default |

The fail-fast policy on `cursorOnHover` and a missing `cursorDefault` is intentional: a wrong configuration should crash early and loudly so the designer sees the issue during development, not as a silent "wrong cursor" in production. See `architecture _context.md` for the fail-fast stance in the framework core.

---

## File layout

```
CursorSystem/
├── CursorController.cs                       # static bus, pack selection, Input System subscriptions, priority cascade
├── CursorSettings.cs                         # SettingsPreset (SO) with a CursorResolutionPack array + OnValidate
├── MouseHoverListener.cs                     # MonoBehaviour for UGUI zones
├── MouseKeyMap.cs                            # POCO model: BoolData/IntData with owner protection
├── SettingsModelExt/
│   ├── CursorPack.cs                         # set of 4 sprites (in the settings assembly)
│   ├── CursorResolutionPack.cs               # CursorPack + maxScreenHeight threshold (in the settings assembly)
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel with the CursorPacks field
│   └── ru.vortex.settings.asmref             # injects the types + partial into the settings assembly
└── ru.vortex.unity.cursorsystem.asmdef
```
