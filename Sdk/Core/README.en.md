# GameCore

**Namespace:** `Vortex.Sdk.Core.GameCore`
**Assembly:** `ru.vortex.sdk.game.core`

## Purpose

Central game session bus. Manages game states (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`), stores a composite data model, and provides a unified API for all subsystems working with gameplay.

Capabilities:
- Game lifecycle management: start, pause, exit
- Implements `IReactiveData` — subscription via `OnUpdate` / `OnUpdateData`
- Composite `GameModel` — extensible container via `IGameData`
- Session service registry (`IGameSessionService`) — awaits external engines' readiness before `Play`
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
├── SessionServices (HashSet<IGameSessionService>)
│                                       ← engines whose readiness GameController
│                                          waits for before entering Play
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
| `IGameSessionService` | `interface` | Game session service contract — `IsReady` + `Name` |
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
- `IGameSessionService` — contract for a service whose readiness `GameController` awaits before `Play`

### Output
- `GameController.GetState()` — current state
- `GameController.Get<T>()` — access to registered data
- `GameController.RegisterSessionService(service)` / `UnregisterSessionService(service)` — session service registration
- `GameController.OnGameStateChanged` — state change event
- `GameController.OnNewGame` — new game event
- `GameController.OnLoadGame` — load complete event
- `GameController.OnUpdate` — static data update subscription (proxies `OnUpdateData`)
- `GameController.CallUpdateEvent()` — invoke `OnUpdateData` with batching via `TimeController.Accumulate`
- `GameController.NewGameAsync(token)` — async variant of `NewGame()` that awaits session services

### Guarantees
- `NewGame()` is blocked until `ExitGame()` is called (lock mechanism)
- Before transitioning to `Play` (after `NewGame` and `OnLoad`), `GameController` waits for all registered `IGameSessionService` to become ready, with no timeout (fail-fast)
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
GameController.NewGame();           // Off → Loading → (await IGameSessionService) → Play, triggers OnNewGame
GameController.SetPause(true);      // Play → Paused
GameController.SetPause(false);     // Paused → Play
GameController.ExitGame();          // → Off, unlocks NewGame
```

`NewGame()` is a sync wrapper over `NewGameAsync()`. Several frames may pass between the call and the actual transition to `Play` if any session services are registered. To await completion explicitly:

```csharp
await GameController.NewGameAsync(cancellationToken);
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
Load: Off → Loading → Init() → Deserialize(POCO) → (await IGameSessionService) → Play → OnLoadGame
```

- `Init()` creates model structure (all `IGameData` implementations via `Activator.CreateInstance`)
- `Deserialize` loads POCO fields into existing objects (does not recreate the dictionary)
- Non-POCO fields (events, references) are preserved from `Init()`
- `GameModel.BeforeDeserialization` creates an `Index` backup in case deserialization fails
- Before transitioning to `Play`, all registered `IGameSessionService` are awaited (see below)

### Session Services

Any package whose readiness is required before a game session begins (engine init, remote config, asset provider, etc.) implements `IGameSessionService` and registers itself with `GameController`. `GameController` then awaits `IsReady = true` before every transition to `Play` after `NewGame` / `OnLoad`.

```csharp
public sealed class MyEngineSessionService : IGameSessionService
{
    public bool IsReady => MyEngine.Initialized;
    public string Name => "MyEngine";

    [RuntimeInitializeOnLoadMethod]
    private static void Register()
        => GameController.RegisterSessionService(new MyEngineSessionService());
}
```

Polling interval — 100ms. **There is no timeout** (fail-fast): a service that never reports ready will block the transition to `Play` forever — preferable to a silent entry into `Play` with an unready engine and hidden bugs in production builds. The debug log indicates which service is being awaited: `[GameController] Awaiting session service: {Name}`.

A built-in example — `NaniSessionService` in `Vortex.NaniExtensions`: registers the Naninovel engine as a session service. Previously the wait for `Engine.Initialized` was hard-wired into `Sdk/Core` — now Sdk/Core has no knowledge of Naninovel.

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
| `IGameSessionService.IsReady` is permanently `false` | Loader hangs in `Loading` until token cancellation (fail-fast) |
| Service not registered, registry empty | `WaitForSessionServices` returns immediately |
| Cancellation while awaiting services | `OperationCanceledException` propagates, `Play` does not occur |
