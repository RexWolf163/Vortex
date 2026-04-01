# GameCore

**Namespace:** `Vortex.Sdk.Core.GameCore`
**Assembly:** `ru.vortex.sdk.game.core`

## Purpose

Central game session bus. Manages game states (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`), stores a composite data model, and provides a unified API for all subsystems working with gameplay.

Capabilities:
- Game lifecycle management: start, pause, exit
- Reactive data model with `Subscribe` / `Unsubscribe`
- Composite `GameModel` — extensible container via `IGameData`
- Automatic pause on application focus loss
- Serialization / deserialization of state
- Editor mode: model creation without running the application

Out of scope:
- Specific game mechanics
- Visual presentation
- Disk persistence (handled by `SaveSystem` from Core)

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`, `IReactiveData`
- `Vortex.Core.System.Abstractions.ReactiveValues` — reactive values
- `Vortex.Core.AppSystem.Bus` — `App`, `AppStates`
- `Vortex.Core.ComplexModelSystem` — `ComplexModel<T>`

### Unity
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`

## Architecture

```
GameController (Singleton, static API)
├── GameModel (ComplexModel<IGameData>)
│   ├── State: GameStates
│   └── Dictionary<Type, IGameData>   ← packages register their data
├── OnNewGame                          ← new game event
├── OnGameStateChanged                 ← state change event
├── Subscribe / Unsubscribe            ← reactive data subscription
└── Serialize / Deserialize            ← JSON model serialization
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `GameController` | `Singleton<T>`, partial, static | Game management bus |
| `GameModel` | `ComplexModel<IGameData>` | Composite data model |
| `GameStates` | `enum` | Off, Play, Win, Fail, Paused, Loading |
| `GameStateHandler` | `MonoBehaviour` | `UIStateSwitcher` by game state |
| `GameStateCondition` | `UnityUserInterfaceCondition` | UI display condition by state |
| `GameMenuHandler` | `MonoBehaviour` | Menu button handler (NewGame, Pause, Exit) |

### Partial Extensions

`GameController` is a partial class. Other packages extend it without modifying the main file:
- `QuestControllerExtEditor` subscribes to `OnEditorGetData`
- Projects can add their own partial extensions

## Contract

### Input
- `App.OnStateChanged` — reaction to global application states
- `GameModel.IGameData` — marker for registering data in the composite model

### Output
- `GameController.GetState()` — current state
- `GameController.Get<T>()` — access to registered data
- `GameController.OnGameStateChanged` — state change event
- `GameController.OnNewGame` — new game event
- `GameController.Subscribe(Action)` — data update subscription

### Guarantees
- `NewGame()` is blocked until `ExitGame()` is called (lock mechanism)
- On `AppStates.Unfocused` — automatic pause
- On `AppStates.Stopping` — resource cleanup
- Setting the same state — ignored (no redundant events)

### Constraints
- One `GameController` instance per application
- `ExitGame()` is required before a subsequent `NewGame()`
- `_data` is lazily created — fail-fast on `GetState()` before initialization

## Usage

### Starting and Ending a Game

```csharp
GameController.NewGame();           // Off → Play, triggers OnNewGame
GameController.SetPause(true);      // Play → Paused
GameController.SetPause(false);     // Paused → Play
GameController.ExitGame();          // → Off, unlocks NewGame
```

### Registering Package Data

```csharp
public class MyPackageData : GameModel.IGameData
{
    public int Score { get; set; }
}

// In the package controller:
var data = GameController.Get<MyPackageData>();
```

### Subscribing to Data Changes

```csharp
GameController.Subscribe(OnDataUpdated);
// ...
GameController.Unsubscribe(OnDataUpdated);
```

### Subscribing to States

```csharp
GameController.OnGameStateChanged += () =>
{
    var state = GameController.GetState();
    // ...
};
```

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `NewGame()` without prior `ExitGame()` | Call is ignored (lock) |
| `SetPause(true)` when `State != Play` | Ignored |
| `SetPause(false)` when `State != Paused` | Ignored |
| `GetState()` before first initialization | NRE — fail-fast by design |
| `Get<T>()` for unregistered type | Returns `null` from `ComplexModel` |
| Focus loss (`Unfocused`) | Automatic `SetPause(true)` |
| `Stopping` | Controller `Dispose()` |
| Editor mode (not Play Mode) | `GetData()` creates a temporary model, invokes `OnEditorGetData` |
