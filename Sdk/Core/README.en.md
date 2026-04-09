# GameCore

**Namespace:** `Vortex.Sdk.Core.GameCore`
**Assembly:** `ru.vortex.sdk.game.core`

## Purpose

Central game session bus. Manages game states (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`), stores a composite data model, and provides a unified API for all subsystems working with gameplay.

Capabilities:
- Game lifecycle management: start, pause, exit
- Implements `IReactiveData` — subscription via `OnUpdate` / `OnUpdateData`
- Composite `GameModel` — extensible container via `IGameData`
- Automatic pause on application focus loss
- Save and load via `ISaveable` / `SaveController`
- Serialization / deserialization of state (POCO fields via `SerializeController`)
- Editor mode: model creation without running the application

Out of scope:
- Specific game mechanics
- Visual presentation
- Disk persistence (handled by `SaveSystem` from Core)

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData`
- `Vortex.Core.AppSystem.Bus` — `App`, `AppStates`
- `Vortex.Core.ComplexModelSystem` — `ComplexModel<T>`
- `Vortex.Core.SaveSystem` — `SaveController`, `ISaveable`

### Unity
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`
- `Cysharp.Threading.Tasks` — `UniTask` (save/load)

## Architecture

```
GameController (Singleton, IReactiveData, ISaveable, static API)
├── GameModel (ComplexModel<IGameData>)
│   ├── State: GameStates
│   └── Dictionary<Type, IGameData>   ← packages register their data
├── OnNewGame                          ← new game event
├── OnGameStateChanged                 ← state change event
├── OnLoadGame                         ← load complete event
├── OnUpdate / OnUpdateData            ← reactive subscription (IReactiveData)
├── CallUpdateEvent()                  ← batching via TimeController.Accumulate
└── Serialize / Deserialize            ← JSON serialization (POCO fields)
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `GameController` | `Singleton<T>`, `IReactiveData`, `ISaveable`, partial, static | Game management bus |
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
- `GameController.OnLoadGame` — load complete event
- `GameController.OnUpdate` — static data update subscription (proxies `OnUpdateData`)
- `GameController.CallUpdateEvent()` — invoke `OnUpdateData` with batching via `TimeController.Accumulate`

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
// Recommended (static event)
GameController.OnUpdate += OnDataUpdated;
GameController.OnUpdate -= OnDataUpdated;

// Trigger update with batching (multiple calls per frame collapse into one)
GameController.CallUpdateEvent();
```

### Subscribing to States

```csharp
GameController.OnGameStateChanged += () =>
{
    var state = GameController.GetState();
    // ...
};
```

### Save and Load

`GameController` implements `ISaveable` and auto-registers with `SaveController`.

```
Load: Off → Loading → Init() → Deserialize(POCO) → Play → OnLoadGame
```

- `Init()` creates model structure (all `IGameData` implementations via `Activator.CreateInstance`)
- `Deserialize` loads POCO fields into existing objects (does not recreate the dictionary)
- Non-POCO fields (events, references) are preserved from `Init()`
- `GameModel.BeforeDeserialization` creates an `Index` backup in case deserialization fails

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
