# StateSwitcher

**Namespace:** `Vortex.Unity.UI.StateSwitcher`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

State machine for visual and behavioral switching. `UIStateSwitcher` manages an array of named states, each containing a set of `StateItem`.

Capabilities:
- Switching by index, name, or enum
- 7 built-in StateItem types: GameObjects, Animator (bool/int), Colors (with animation), Sprites, Events, TweenerHub
- `StateSwitcherAttribute` for enum binding with Inspector visualization
- `OnStateSwitch` event on state change
- Reentrancy protection

Out of scope:
- Transition animations between states (use `TweenerHubSwitch` or `ColorsSwitch`)
- State selection logic (layer 3/4)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` — integration via `TweenerHubSwitch` |
| `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` | `AsyncTween` — color animation in `ColorsSwitch` |
| `Vortex.Unity.EditorTools` | `DrawingUtility`, `ToolsSettings` — `StateSwitcherDrawer` rendering |
| `Vortex.Core.Extensions` | `ActionExt.Fire()` |
| Odin Inspector | `[ValueDropdown]`, `[ShowInInspector]`, `[Button]` |

---

## Architecture

```
StateSwitcher/
├── UIStateSwitcher.cs          # State machine (MonoBehaviour)
├── StateItem.cs                # Abstract behavior base
├── Items/
│   ├── GameObjectsSwitch.cs    # SetActive on GameObject array
│   ├── AnimatorBoolSwitch.cs   # SetBool on Animator
│   ├── AnimatorStateSwitch.cs  # SetInteger on Animator
│   ├── ColorsSwitch.cs         # Color (instant or animated)
│   ├── SpritesSwitch.cs        # Sprite on SpriteRenderer/Image
│   ├── EventFire.cs            # UnityEvent on activation
│   ├── TweenerHubSwitch.cs     # Forward/Back on TweenerHub
│   └── TweenersSwitch.cs       # [Obsolete] Legacy TweenerBase
└── Handlers/
    └── OnEnableStateRunner.cs  # Switch on OnEnable
```

### UIStateSwitcher

MonoBehaviour with a `StateData` array. Each `StateData` is a named state with a `StateItem[]` array.

On switch:
1. Calls `DefaultState()` on all items of the previous state (**Danger point!**)
2. Calls `Set()` on all items of the new state
3. Fires `OnStateSwitch` event

---

## API

```csharp
switcher.Set(0);                        // by index
switcher.Set("Active");                 // by name
switcher.Set(MyEnum.Active);            // by enum
switcher.Set((byte)2);                  // by byte
switcher.ResetStates();                 // reset all items + initial state

int current = switcher.State;           // current state (-1 = none)
switcher.OnStateSwitch += OnSwitch;     // event (StateData or null)
int index = switcher.GetState("name");  // index by name (-1 = not found)
```

| Inspector Field | Type | Description |
|-----------------|------|-------------|
| `states` | `StateData[]` | State array |
| `stateOnEnable` | `int` | Initial state (dropdown) |

---

## StateItem

Abstract behavior class. On state activation — `Set()`, on deactivation — `DefaultState()`.

| Implementation | Set() | DefaultState() |
|---------------|-------|----------------|
| `GameObjectsSwitch` | `SetActive(true)` on array | `SetActive(false)` |
| `AnimatorBoolSwitch` | `SetBool(name, true)` | `SetBool(name, false)` |
| `AnimatorStateSwitch` | `SetInteger(name, value)` | `SetInteger(name, default)` |
| `ColorsSwitch` | Set color (instant or via `AsyncTween`) | Revert to `Color.white` |
| `SpritesSwitch` | Set sprite | Set `null` |
| `EventFire` | `UnityEvent.Invoke()` | Nothing |
| `TweenerHubSwitch` | `tweener.Forward()` | `tweener.Back()` |

### ColorsSwitch

Supports SpriteRenderer, Graphic (Image, Text), Outline. Modes:
- Instant color change
- Animated via `AsyncTween` with configurable duration and curve
- Optional start color: `_useOwnStartColor = true` — takes current color from the object, `false` — uses the `_startColor` field

Inspector button `SetCurrent()` — captures current color from first object.

---

## StateSwitcherAttribute

Attribute for binding `UIStateSwitcher` to an enum. Used with `StateSwitcherDrawer`.

```csharp
[SerializeField, StateSwitcher(typeof(MyStatesEnum))]
private UIStateSwitcher switcher;
```

In Inspector displays a state table:
- Index, description (from `[Tooltip]` / `[LabelText]`), name from switcher
- Active state highlighting
- Click to switch
- **Sync** button — synchronizes state names with enum descriptions

---

## OnEnableStateRunner

MonoBehaviour — switches a `UIStateSwitcher` to a given state on `OnEnable`.

| Field | Type | Description |
|-------|------|-------------|
| `_stateSwitcher` | `UIStateSwitcher` | Target switcher |
| `_stateToOpen` | `int` | State index |

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `Set()` with out-of-range index | `LogError`, state unchanged |
| `Set()` with same index | Skipped (state not reapplied) |
| `Set()` during transition | Reentrancy guard (`_isSwitching`) |
| `Set()` before `Awake` | State saved in `_startState`, applied in `Awake` |
| `GetState("nonexistent")` | Returns -1 |
| `Set("nonexistent")` | `LogError`, state unchanged |
| `ColorsSwitch` with `_smoothChange` and zero `_duration` | Instant change (AsyncTween with duration ≤ 0) |
| Duplicate `DropDownGroupName/DropDownItemName` across StateItem classes | `LogError`, duplicate skipped in dropdown |
