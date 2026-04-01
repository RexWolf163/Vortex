# AppSystem (Unity)

Unity adaptation of the application lifecycle.

## Purpose

Bridges Unity lifecycle with the `App` bus: focus/background handling, process termination, debug settings.

- Translates `OnApplicationFocus` / `OnApplicationPause` into `App` states
- Application termination on `Stopping` (`Application.Quit` / `EditorApplication.isPlaying`)
- Calls `App.Exit()` on `AppStateHandler` destruction
- Debug toggle for state transition logging

Out of scope: state transition logic, events, data model — these belong to Core (Layer 1).

## Dependencies

- `Vortex.Core.AppSystem` — `App` bus, `AppStates`
- `Vortex.Unity.EditorTools.Attributes` — `[ToggleButton]` (for `DebugSettings`)
- `UnityEngine` — `MonoBehaviour`, `Application.Quit()`

## Architecture

```
AppStateHandler (MonoBehaviour)
├── Awake()              — subscribe to App.OnStateChanged
├── OnApplicationFocus() — _pauseState → SetPauseState()
├── OnApplicationPause() — _pauseState → SetPauseState()
├── OnStateChanged()     — update _oldState, handle Stopping
├── OnDestroy()          — App.Exit()
└── Start() [Editor]     — _started delay (1s)

DebugSettingsExtApp (partial DebugSettings)
└── AppStateDebugMode    — DebugMode && appStates
```

---

## AppStateHandler

MonoBehaviour bridging Unity lifecycle with the `App` bus.

### Contract

**Input:**
- Unity lifecycle events: `OnApplicationFocus`, `OnApplicationPause`, `OnDestroy`
- `App.OnStateChanged` event

**Output:**
- `App.SetState()` calls on focus/background changes
- `App.Exit()` on component destruction
- `Application.Quit()` on `Stopping` state

### Logic

| Event | Action |
|-------|--------|
| `OnApplicationFocus(false)` | `App.SetState(Unfocused)` |
| `OnApplicationPause(true)` | `App.SetState(Unfocused)` |
| Focus restored | `App.SetState(_oldState)` — restores previous state |
| `OnStateChanged(Stopping)` | `Application.Quit()` (in editor: `EditorApplication.isPlaying = false`) |
| `OnStateChanged(Unfocused)` | Ignored — `_oldState` not updated |
| `OnStateChanged(other)` | `_oldState = newState` |
| `OnDestroy` | `App.Exit()` |

### Editor Protection

The `_started` flag is set 1 second after `Start` (coroutine). `OnDestroy` before this moment is ignored — protection against false triggering when launching with an active scene, where Unity may recreate objects.

---

## DebugSettingsExtApp

Partial extension of `DebugSettings`. Adds an `appStates` toggle with the `[ToggleButton]` attribute.

The `AppStateDebugMode` property is `true` only when both global `DebugMode` and local `appStates` are enabled. Used in `App.SetState()` for conditional transition logging.

---

## Usage

### Scene Setup

Place `AppStateHandler` on a persistent GameObject (not destroyed on scene changes). The component automatically subscribes to `App.OnStateChanged` in `Awake`.

### Debug

In the `DebugSettings` asset, enable `DebugMode` (global) and `appStates` (local). All state transitions will then be logged in `App.SetState()`.

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Multiple consecutive `Unfocused` | `_oldState` not updated — last working state will be restored |
| `OnDestroy` in first second (Editor) | Ignored — `_started` is still `false` |
| `Stopping` in Editor | `EditorApplication.isPlaying = false` instead of `Application.Quit()` |
| `OnDestroy` on normal exit | `App.Exit()` → `Stopping` → `OnExit` |
