# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Assembly:** `ru.vortex.unity.cursorsystem`

---

## Purpose

Custom cursor for UGUI projects: default sprite, separate sprites for LMB/RMB, and an array of hover variants switched by UI zones. Applying a sprite to the system cursor goes through `Cursor.SetCursor`; mouse events go through Unity Input System (no polling).

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
| `Vortex.Unity.UI.UIComponents` | (optional, via `UIComponent` in consumers) |
| `Unity.InputSystem` | `InputAction` for LMB/RMB |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` in `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]` |

`SettingsModelExt/ru.vortex.settings.asmref` injects the partial extension of the settings model into the `ru.vortex.settings` assembly so cursor fields live in the unified `SettingsModel`.

---

## Architecture

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorDefault / cursorLeftMouseDown / cursorRightMouseDown / cursorOnHover[]
           │
           │  (via Settings.OnInit + partial SettingsModel)
           ▼
[Settings.Data() in SettingsModel]
   └── CursorDefault, CursorLeftMouseDown, CursorRightMouseDown, CursorOnHover[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — reads the settings, raises the InputActions
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(index) / OnUnHover(index) ← public API from the view layer
   └── ApplyByPriority() — LMB > RMB > Hover > Default → Cursor.SetCursor

[MouseHoverListener] (MonoBehaviour, on UGUI objects)
   └── IPointerEnter/Exit → CursorController.OnHover(index) / OnUnHover(index)

[MouseKeyMap] (POCO, exposed via CursorController.MouseKeys)
   ├── BoolData LeftKeyPressed
   ├── BoolData RightKeyPressed
   └── IntData  HoverIndex     (-1 = no hover)
```

### Sprite priority

`ApplyByPriority` (in `CursorController.cs`) implements an explicit cascade with `return` statements:

1. **LMB pressed + `cursorLeftMouseDown` non-null** → apply the LMB sprite.
2. **RMB pressed + `cursorRightMouseDown` non-null** → apply the RMB sprite.
3. **`HoverIndex >= 0` + `cursorOnHover[HoverIndex]` non-null** → apply the hover sprite.
4. Otherwise → default sprite.

LMB overrides RMB and hover. RMB overrides hover. Hover is active only when no mouse button is pressed.

### Hardware cursor

If `cursorDefault` is not set in `CursorSettings`, `Init()` returns early, no `InputAction`s are created, `Cursor.SetCursor` is never called — the cursor stays as the OS-native one. This lets you globally disable the custom cursor by nulling a single `[SerializeField]`, without touching code.

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

Fill in:
- `cursorDefault` — main sprite (required to activate the system).
- `cursorLeftMouseDown` / `cursorRightMouseDown` — optional, for click feedback.
- `cursorOnHover[]` — array of sprites for different UI-zone types (over a button, an inventory icon, a link, etc.).

### 2. Attach `MouseHoverListener` to a UGUI element

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (index is picked from the dropdown in the inspector)
```

The inspector dropdown reads sprite names from the active `CursorSettings` — designers don't memorize indices, they pick by name. The `[NONE]` entry (`-1`) disables hover switching for that zone (useful when the zone must receive clicks but shouldn't change the cursor).

### 3. Programmatic hover

If the hover zone is not UGUI (a world-space collider, a custom raycast, a hotkey emulator) — call directly:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private int cursorIndex = 0;

    private void OnMouseEnter() => CursorController.OnHover(cursorIndex);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorIndex);
}
```

### 4. Subscribing to mouse state externally

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
| `cursorDefault == null` in settings | The controller does not activate, the cursor stays system-native |
| `cursorLeftMouseDown == null` with LMB pressed | Skip the branch, continue down the priority chain (RMB → Hover → Default) |
| `cursorOnHover` is empty or `null` | Any `OnHover(index >= 0)` → `IndexOutOfRangeException` (fail-fast: invalid configuration) |
| `OnHover(index)` with an index outside the array | `IndexOutOfRangeException` (fail-fast) |
| `cursorOnHover[index] == null` (valid index, but the sprite is missing) | Fall back to default |
| Alt-tab / focus loss with a button held | Input System sends `canceled` → state resets → cursor reverts to default |
| Nested hover zones (A contains B) | `Enter(B)` sets the index to B; a late `Exit(A)` is ignored (race) |
| Reinitialisation (restart without domain reload) | Old `InputAction`s are released before the new ones are raised |
| `App.OnExit` | `DisposeActions` releases the `InputAction`s cleanly |
| Scene opened without an active `EventSystem` | `MouseHoverListener` receives no Enter/Exit — the cursor only reacts to LMB/RMB and default |

The fail-fast policy on `cursorOnHover` is intentional: a wrong array configuration should crash early and loudly so the designer sees the issue during development, not as a silent "wrong cursor" in production. See `architecture _context.md` for the fail-fast stance in the framework core.

---

## File layout

```
CursorSystem/
├── CursorController.cs                       # static bus, Input System subscriptions, priority cascade
├── CursorSettings.cs                         # SettingsPreset (SO) with 4 sprite fields
├── MouseHoverListener.cs                     # MonoBehaviour for UGUI zones
├── MouseKeyMap.cs                            # POCO model: BoolData/IntData with owner protection
├── SettingsModelExt/
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel with cursor fields
│   └── ru.vortex.settings.asmref             # injects the partial extension into the settings assembly
└── ru.vortex.unity.cursorsystem.asmdef
```
