# AppSystem (Core)

Application lifecycle finite state machine.

## Purpose

Application state management through a static bus, event-driven transitions, deferred initialization until `Settings` is ready.

- Finite state machine (`AppStates`)
- Transition events (`OnStateChanged`, `OnStarting`, `OnStart`, `OnExit`)
- Deferred initialization until `Settings` readiness
- Conditional transition logging via debug flag
- Startup time recording

Out of scope: focus/background handling, process termination, scene loading, resource management, saving.

## Dependencies

- `Vortex.Core.SettingsSystem` — readiness check (`Settings.Data()`), debug flag (`AppStateDebugMode`)
- `Vortex.Core.LoggerSystem` — transition logging
- `Vortex.Core.System.Enums` — `AppStates`

## Architecture

```
App (static partial bus)
├── App.cs          — AppModel, Init(), Exit()
└── AppExtEvents.cs — events, GetState(), SetState()

AppModel (sealed partial)
├── AppModel.cs        — _state, _startTime
└── AppModelExtTime.cs — GetStartTime()

SettingsModelExtDebug (partial SettingsModel)
└── AppStateDebugMode  — bool property
```

### States

```
None → WaitSettings (if Settings not ready)
None → Starting → Running ⇄ Unfocused
                → Loading
                → Saving
       Running  → Stopping
```

| State | Description |
|-------|-------------|
| `None` | Before first access to `App` |
| `WaitSettings` | `Settings` not loaded, initialization deferred |
| `Starting` | Startup, system loading |
| `Running` | Normal operation |
| `Loading` | Data loading |
| `Saving` | Data saving |
| `Unfocused` | Application in background |
| `Stopping` | Shutting down |

### Initialization

The `App.Data` getter creates `AppModel` on first access and calls `SetState(Starting)`. If `Settings.Data() == null`, state transitions to `WaitSettings`. On subsequent access to `Data` while in `WaitSettings`, initialization retries.

### Transitions (SetState)

1. Duplicate state — `return false`
2. `Settings.Data() == null` — transition to `WaitSettings`, `return false`
3. Logging (when `AppStateDebugMode` is active)
4. Set new state, invoke `OnStateChanged`
5. `Starting|Unfocused → Running` — additionally invoke `OnStart`
6. `→ Starting` — `OnStarting`
7. `→ Stopping` — `OnExit`

## Contract

### Input
- `Settings` readiness (via `Settings.Data()`)

### Output
- Current state: `App.GetState()`
- Events: `OnStateChanged(AppStates)`, `OnStarting`, `OnStart`, `OnExit`
- Startup time: `AppModel.GetStartTime()`

### Guarantees
- `SetState` is idempotent — setting the same state returns `false`
- `OnStart` is invoked only on `Starting|Unfocused → Running` transition
- Initialization defers until `Settings` readiness
- `_startTime` is recorded as `DateTime.UtcNow` on `AppModel` creation

### Limitations
- `SetState` accesses `_data` directly — calling before first `Data` access causes NRE
- `AppModel` is `sealed` with `internal` constructor — creation only within `App`

## Usage

### State subscription

```csharp
App.OnStateChanged += (AppStates newState) => { };
App.OnStarting += () => { };    // → Starting
App.OnStart += () => { };       // Starting|Unfocused → Running
App.OnExit += () => { };        // → Stopping
```

### State management

```csharp
AppStates state = App.GetState();
bool changed = App.SetState(AppStates.Running);
App.Exit(); // → Stopping → OnExit
```

### Debug

Transitions are logged automatically when debug mode is active. The `AppStateDebugMode` flag is a partial extension of `SettingsModel`. Configuration: enable `DebugMode` (global) and `appStates` (local toggle) in the `DebugSettings` asset.

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `Settings` not ready on first access | State becomes `WaitSettings`, retries on next `Data` access |
| Duplicate `SetState` with same value | `return false`, no events fired |
| `OnStart` on return from `Unfocused` | Invoked if new state is `Running` |
| `SetState` before `AppModel` creation | NRE — `_data` is still `null` |
