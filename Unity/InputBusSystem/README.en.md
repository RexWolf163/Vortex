# InputBusSystem

**Namespace:** `Vortex.Unity.InputBusSystem`
**Assembly:** `ru.vortex.unity.inputbussystem`

## Purpose

Input management built on Unity Input System with support for Input Action Maps, LIFO-based action subscriptions, and direct key bindings.

Capabilities:
- Centralized input signal routing through a static controller
- Input map management with automatic activation/deactivation based on subscriber presence
- LIFO routing — only the last registered subscriber receives the signal
- Guaranteed `performed`/`canceled` pairing — cancellation is delivered only to the recipient of the press
- Binding arbitrary keys and combinations to UI events without direct Input System API interaction

Out of scope:
- Runtime key rebinding
- Visual input map editor (standard Unity Input Actions Asset is used)
- Analog input in **routing** — the bus routes only discrete actions (Button) via events; analog values are read by polling through `GetAction` (see Usage)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| Unity Input System | `InputSystem.actions`, `InputAction`, `InputActionMap` |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `App.OnExit` |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` — diagnostic logging |
| `Vortex.Core.Extensions` | `AddNew`, `AddOnce` — collection helpers |
| `Vortex.Unity.EditorTools` | `[ValueSelector]`, `[ClassLabel]` |
| `Vortex.Unity.UI.Misc` | `AdvancedButton` — optional integration |

---

## Architecture

```
InputBusSystem/
├── InputController.cs          # Static controller: indexing, routing
├── InputSubscriber.cs          # Subscriber model with Owner-based equality
├── Handlers/
│   ├── InputActionHandler.cs   # Action subscription via InputController
│   ├── InputPointerHandler.cs  # Emits the pointer position (screen) on every action event
│   ├── InputMapHandler.cs      # Input map activation
│   └── KeyboardHandler.cs      # Direct key/combination binding
└── Debug/
    ├── Model/SettingsModelExtInput.cs    # partial SettingsModel + InputDebugMode
    └── Presets/DebugSettingsExtInput.cs  # partial DebugSettings + toggle
```

### Data Flow

```
Unity Input System
    ↓ performed / canceled
InputController (LIFO routing)
    ↓ callback
InputActionHandler / subscriber
    ↓ UnityEvent
UI / game logic
```

`KeyboardHandler` operates autonomously — creates its own `InputAction` bypassing `InputController`.

---

## Key Concepts

### Composite action id (`Map/Action`)

An action is indexed by a composite id `Map/Action` (via `/`), not by its bare name. This:
- removes the clash of same-named actions in different maps (ids differ, `Init` no longer throws on a duplicate);
- lays the dropdown out into map folders — `ValueSelector` via `SearchablePopup` groups values by `/`.

The unique-names philosophy is preserved: what is unique now is the `map/action` pair, not the bare action name.

### LIFO Routing

With multiple subscribers on the same action, `performed` is delivered only to the last one (`subscribers[^1]`). Allows input interception in modal windows and overlays without unsubscribing previous layers.

### Performed/Canceled Pairing

The object that received `performed` is stored in `CatchPerformed`. On `canceled`, the signal is delivered only to that object. If the top subscriber has changed — `canceled` is discarded.

### Maps with User Counting

A map is activated on the first `AddMapUser` and deactivated when the last user calls `RemoveMapUser`. Prevents map conflicts without manual management.

### Key Combinations

`KeyboardHandler` supports up to 3 modifiers via Unity composite bindings (`OneModifier`, `TwoModifiers`, `ThreeModifiers`). The last key in the array is the trigger, the rest are modifiers.

### Deferred Subscription

Handlers check `App.GetState() < AppStates.Running` and defer registration until the application is ready via `App.OnStateChanged`.

---

## Contract

### Input
- Unity Input Actions Asset with defined maps and actions
- `AppStates.Running` — the state at which handlers register

### Output
- `UnityEvent onPressed` / `onReleased` — in `InputActionHandler` and `KeyboardHandler`
- `AdvancedButton.Press()` / `Release()` — optional integration
- `InputActionMap` activation/deactivation — in `InputMapHandler`

### Guarantees
- One `performed` → at most one `canceled` for the same subscriber
- A map is deactivated only when user count reaches zero
- A subscriber is not duplicated in the list (`AddOnce` with Owner-based equality)
- Handlers unsubscribe in `OnDisable` / `OnDestroy`

### Constraints
- Routing is discrete input only (Button); analog values are not transmitted via events, but are available by polling through `GetAction`
- `InputSubscriber.Equals` compares by `Owner` — one object cannot have different callbacks for the same action
- `KeyboardHandler` supports a maximum of 3 modifiers; 4+ are ignored with a warning
- No priority mechanism — only insertion order (LIFO)

---

## API

### InputController (static)

```csharp
// Input maps
static string[] GetMaps()
static void AddMapUser(string mapId, object inputMapUser)
static void RemoveMapUser(string mapId, object inputMapUser)

// Input actions
static string[] GetActions()
static void AddActionUser(string actionInputId, object actionInputUser,
    Action onPerformedCallback, Action onCanceledCallback)
static void RemoveActionUser(string actionInputId, object actionInputUser)

// Direct InputAction access for polling reads (ReadValue<T>()/IsPressed())
static InputAction GetAction(string actionInputId)   // null — key not found
```

### InputSubscriber

```csharp
// Equality determined by Owner
public readonly object Owner;
public Action OnPerformed { get; }
public Action OnCanceled { get; }
```

---

## Usage

### Action subscription from code

```csharp
InputController.AddActionUser("Player/Jump", this, OnJumpPerformed, OnJumpCanceled);

// Unsubscribe
InputController.RemoveActionUser("Player/Jump", this);
```

`this` serves as the identification key. A repeated `AddActionUser` with the same object will not create a duplicate.

### Polling a value (GetAction)

For analog input (sticks, triggers, cursor delta), where you need the value on demand rather than a callback subscription, `GetAction("Map/Action")` returns the `InputAction` directly — then `ReadValue<T>()` / `IsPressed()` in your own `Update`:

```csharp
var move = InputController.GetAction("Player/Move");
var delta = move?.ReadValue<Vector2>() ?? Vector2.zero;
```

`null` — key not found. This is an escape hatch around the LIFO routing: the caller reads the value itself, the bus is not involved in polling (in the editor the method runs `Init` on demand if needed).

### Subscription via Inspector (InputActionHandler)

`InputActionHandler` component on a GameObject:
- `inputAction` — action from dropdown (composite id `Map/Action`, grouped into map folders)
- `button` — optional reference to `AdvancedButton`
- `onPressed` / `onReleased` — Unity Events

### Pointer position (InputPointerHandler)

`InputPointerHandler` subscribes to an action by the bus rules (LIFO) and, on **every** event of it, emits the current pointer position through a single `UnityEvent<Vector2>` (`onPoint`, screen coordinates via `Pointer.current`). Unlike `InputActionHandler` (two events, press/release), this is one method carrying a vector value; what to do with the point and when to start/stop catching is up to the subscriber (e.g. by enabling/disabling the object via a press action).

- `inputAction` — the action whose event = the moment to emit the position (e.g. `CursorPosition`)
- `onPoint` — `UnityEvent<Vector2>`, invoked on every action event with the current position

Subscription is deferred until `AppStates.Running` (as in `InputActionHandler`); auto-unsubscribe in `OnDisable`/`OnDestroy`.

### Map activation (InputMapHandler)

`InputMapHandler` component on a GameObject:
- `inputMap` — map from dropdown

The map is active as long as at least one `InputMapHandler` for it is enabled in the scene.

### Direct key binding (KeyboardHandler)

`KeyboardHandler` component on a GameObject:
- `buttonCode` — array of individual keys (any triggers activation)
- `buttonsCombinations` — array of combinations (last key is the trigger, others are modifiers)
- `button`, `onPressed`, `onReleased` — same as `InputActionHandler`

---

## Critical Requirements

1. **Input Actions Asset must be assigned** — `InputController.Init()` reads `InputSystem.actions`. If missing, dictionaries are empty and handlers will throw `KeyNotFoundException`.
2. **The map name and the `Map/Action` pair must be unique.** Actions are indexed by a composite id, so same-named actions in DIFFERENT maps are allowed (their ids differ). A duplicate map name or a fully identical `Map/Action` causes `ArgumentException` during initialization (Unity does not normally produce such a case).
3. **`KeyboardHandler` does not go through `InputController`** — its actions do not participate in LIFO routing.
4. **`KeyboardHandler` requires a physical keyboard** — logs a warning when `Keyboard.current == null`.

---

## Debug

Diagnostic logging for `InputController` is controlled via `Settings.Data().InputDebugMode`; `KeyboardHandler` logs under the general `Settings.Data().DebugMode`. Configured in the `DebugSettings` asset (`InputDebugMode` toggle).

When enabled, logs: subscriber registration/removal, `performed`/`canceled` delivery, map activation/deactivation.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Non-existent action/map name | `KeyNotFoundException` |
| `RemoveActionUser` for unregistered object | Silent skip |
| `performed` subscriber removed before `canceled` | `canceled` discarded |
| No subscribers on `performed` | Silent skip |
| `Keyboard.current == null` | Warning; presses ignored |
| Combination with 4+ modifiers | Warning; combination skipped |
| `OnEnable` before `AppStates.Running` | Subscription deferred until state change |
| Input Actions Asset not assigned | Dictionaries empty; `KeyNotFoundException` on registration |
