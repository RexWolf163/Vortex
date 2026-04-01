# MiniGamesSystem

**Namespace:** `Vortex.Sdk.MiniGamesSystem.*`
**Assembly:** `ru.vortex.sdk.minigames` (framework) + separate assemblies per game

## Purpose

Framework for embedding mini-games into the main project. Provides a unified lifecycle, state management, statistics, and scaffolding for creating new mini-games.

Capabilities:
- Unified lifecycle: initialization → launch → gameplay → completion (win/fail)
- State management (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`) with reactive events
- Automatic pause on application focus loss
- Statistics collection: starts, wins, losses
- View connection via configuration (prefab specified in ScriptableObject)
- Controller substitution via inspector dropdown (direct DI through configuration)
- New mini-game boilerplate generation from `.vtp` template
- Template implementations: Gym, Painting, Puzzle

Out of scope:
- Specific mini-game logic (implemented in controller subclasses)
- Scene/resource loading and unloading
- Networking
- Mini-game progress persistence between sessions (Save/Load is scaffolded but not wired)

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`, `IDataStorage`
- `Vortex.Core.AppSystem.Bus` — `App.OnStateChanged`
- `Vortex.Core.Extensions.LogicExtensions` — string utilities, serialization

### Unity
- `Vortex.Unity.Extensions.Abstractions` — `MonoBehaviourSingleton`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `Timer`, `TimeController`
- `Vortex.Unity.UI` — `UIStateSwitcher`, `UIComponent`, `Pool`, `DataStorage`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `InfoBubble`

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `GameStates`

### External
- **Odin Inspector** — inspector attributes (`InfoBox`, `ValueDropdown`, `GUIColor`)

## Architecture

```
┌─────────────────────────────────────────────┐
│              External code                   │
│     await XxxGameHub.Play(config)            │
│     hub.OnWin += ...                         │
└──────────┬──────────────────┬───────────────┘
           │                  │
           ▼                  ▼
┌────────────────────┐  ┌─────────────────────┐
│       Hub          │  │  MiniGamesController │
│ (MonoBehaviour-    │  │  (statistics,        │
│  Singleton)        │  │   registration)      │
│                    │  └─────────────────────┘
│ - holds config     │
│ - creates Controller
│ - IDataStorage     │
│ - async Play()     │
└────────┬───────────┘
         │ creates (Activator)
         ▼
┌────────────────────┐
│    Controller      │
│ (Singleton, POCO)  │
│                    │
│ - owns Data        │
│ - SetState()       │
│ - game logic       │
│ - reacts to        │
│   AppState         │
└────────┬───────────┘
         │ reads / modifies
         ▼
┌────────────────────┐
│      Data          │
│ (MiniGameData)     │
│                    │
│ - State            │
│ - OnGameStateChanged
│ - OnUpdated        │
│ - game parameters  │
└────────┬───────────┘
         │ subscribes to events
         ▼
┌────────────────────┐
│      View          │
│ (MonoBehaviour)    │
│                    │
│ - gets data via    │
│   IDataStorage     │
│ - displays         │
│ - actions via      │
│   ExtLogic →       │
│   Controller       │
└────────────────────┘
```

Data flow is unidirectional: Controller → Data → View.
Feedback: View → ExtLogic (extension method) → Controller.

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Hub** | Entry point for external code. `MonoBehaviourSingleton`. Holds configuration, creates controller via `Activator.CreateInstance`, implements `IDataStorage`. One instance per mini-game |
| **Controller** | POCO singleton. Owns data model, implements game logic. The only component that changes state via `SetState()` |
| **Data** | `MiniGameData` subclass. Contains state, parameters, events. Modified only by the controller |
| **GeneralConfig** | `ScriptableObject` with settings: controller type (string), View prefab, difficulty levels, timers |
| **ExtLogic** | Static class with extension methods on data models. Routes View actions to Controller without exposing the controller to the View layer |
| **MiniGameViewContainer** | Instantiates the View prefab from configuration and binds it to `IDataStorage` |
| **MiniGameObserver** | Subscribes to Hub events and relays them to `MiniGamesController` for statistics. Guarantees atomic unsubscription |

### Framework Components

| Class | Type | Purpose |
|-------|------|---------|
| `MiniGamesController` | static | Hub registration, statistics collection |
| `MiniGameHub<T,TD,TCf,TC>` | `MonoBehaviourSingleton`, abstract | Base hub |
| `MiniGameController<T,TU>` | `Singleton<T>`, abstract | Base controller |
| `MiniGameData` | abstract | Base data model |
| `MiniGameObserver` | internal | Hub subscriptions → statistics |
| `MiniGameStates` | enum | Off, Play, Win, Fail, Paused, Loading |
| `MiniGameStatisticData` | POCO | Single mini-game statistics |
| `MiniGamesStatisticsData` | `IGameData` | Statistics index for all mini-games |
| `FieldSize` | struct | Field size: columns x rows |
| `IMiniGameConfig` | interface | Configuration contract: GetView(), GetController() |
| `IMiniGameController<T>` | interface | Controller contract: Init, Play, Exit, Pause, Cheats |
| `IMiniGameHub` | interface | Hub contract: OnWin/OnFail/OnStart events |
| `IGameModelWithTimer` | interface | Timer access from model |
| `IHaveGodMode` | interface | God mode (skip HP loss, etc.) |

### Reusable Handlers

| Class | Purpose |
|-------|---------|
| `GameTimerView` | Slider display of timer progress |
| `MiniGameStateSwitcher` | `UIStateSwitcher` by `MiniGameStates` |
| `MockUpStateSwitcher` | Debug state switch button |
| `MiniGameCheatWinHandler` | Cheat win button |
| `MiniGameCheatFailHandler` | Cheat fail button |
| `MiniGameViewContainer` | View instantiation from configuration |

## Contract

### Input
- `ScriptableObject` config: controller, View prefab, difficulty levels
- Hub component on scene with assigned config
- PlayConfig struct with launch parameters (optional)

### Output
- `async UniTask Play()` — completes when state transitions to `Off`
- `OnWin`, `OnFail`, `OnStart` events on Hub
- Statistics in `MiniGamesStatisticsData` via `GameController.Get<>()`

### Guarantees
- On `GameStates.Paused` — automatic mini-game pause
- On `GameStates.Off/Fail/Win/Loading` — mini-game state resets to `Off`
- `MiniGameObserver` guarantees unsubscription on deregistration
- Registration in `MiniGamesController` is automatic in `Awake()` / `OnDestroy()`

### Constraints
- One mini-game instance per application (Hub — Singleton, Controller — Singleton)
- Controller created via `Activator.CreateInstance` — requires public parameterless constructor
- Controller type is specified as string — renaming / assembly change causes `NullReferenceException`
- View prefab must have `DataStorage` on root GameObject
- `Data.State` is modified only via `Controller.SetState()` (except debug tools)

## Usage

### Creating a New Mini-Game from Template

1. Select target folder in the Project window
2. **Assets → Create → Vortex Templates → MiniGame**
3. Enter the mini-game name
4. The scaffolder creates the file structure

### Implementation Structure

Each mini-game follows the pattern:

```
MyGame/
├── Abstractions/
│   └── IMyGameController.cs       ← controller interface
├── Config/
│   └── MyGameGeneralConfig.cs     ← ScriptableObject configuration
├── Controllers/
│   ├── MyGameController.cs        ← game logic
│   └── MyGameDataExtLogic.cs      ← extension methods View → Controller
├── Models/
│   ├── MyGameData.cs              ← data model
│   └── MyGamePlayConfig.cs        ← launch parameters
├── View/
│   └── MyGameFieldView.cs         ← visual representation
├── Editor/
│   └── MyGameMenuController.cs    ← editor utilities
└── Prefabs/
```

### Controller Implementation

```csharp
public class MyGameController : MiniGameController<MyGameController, MyGameData>, IMyGameController
{
    public override void Play()
    {
        Data.StartTimer(OnTimerRunOut);
        Data.OnGameStateChanged -= OnStateLogic;
        Data.OnGameStateChanged += OnStateLogic;
        SetState(MiniGameStates.Play);
    }

    public void PlayerAction(MyPieceData piece)
    {
        // game logic
        if (CheckWinCondition())
            SetState(MiniGameStates.Win);
    }

    private void OnTimerRunOut()
    {
        SetState(CheckWinCondition() ? MiniGameStates.Win : MiniGameStates.Fail);
    }

    private void OnStateLogic(MiniGameStates state)
    {
        switch (state)
        {
            case MiniGameStates.Play:
                Data.Timer.Resume();
                break;
            case MiniGameStates.Off:
                Data.OnGameStateChanged -= OnStateLogic;
                break;
            default:
                if (!Data.Timer.IsComplete)
                    Data.Timer.SetPause();
                break;
        }
    }
}
```

### View → Controller via ExtLogic

```csharp
// ExtLogic
public static class MyGameDataExtLogic
{
    public static void PlayerAction(this MyPieceData piece) =>
        MiniGamesController.GetController<IMyGameController>().PlayerAction(piece);
}

// View calls extension method on data — knows nothing about the controller
private void OnClick() => _data.PlayerAction();
```

### Launching from External Code

```csharp
await MyGameHub.Play(new MyGamePlayConfig { Difficulty = 1 });

MyGameHub.Instance.OnWin += HandleWin;
MyGameHub.Instance.OnFail += HandleFail;
```

## Template Implementations

The package contains three template mini-games — examples of the standardized pattern:

| Game | Assembly | Mechanic |
|------|----------|----------|
| **Gym** | `ru.vortex.minigames.gym` | Timing slider, phases (Ready → Push → Return), trainer animations (Spine / VideoPlayer) |
| **Painting** | `ru.vortex.minigames.paint` | Cell grid, cross-neighbor inversion, object pool |
| **Puzzle** | `ru.vortex.minigames.puzzle` | Texture slicing, Fisher-Yates shuffle, drag & drop with `AsyncTween` |

All implementations follow the same template and serve as reference examples for creating new mini-games.

## Editor Tools

- **Template generator** (`MiniGameTemplateMenu`) — creates full file structure for a new mini-game
- **Controller validation** — configs highlight the controller field red when the type is invalid
- **Array synchronization** — `OnLevelsChanged` automatically adjusts timer, field size, and other array lengths to match the number of difficulty levels
- **Debug buttons** — `MockUpStateSwitcher`, `MiniGameCheatWinHandler`, `MiniGameCheatFailHandler` for testing without playing through

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Config not assigned in Hub | `LogError`, Hub not registered, `Play()` throws exception |
| Controller type not found | `NullReferenceException` on first `Controller` access |
| View prefab without `DataStorage` on root | `NullReferenceException` in `MiniGameViewContainer.Start()` |
| `Play()` while game is active | New state cycle on top of current — behavior undefined |
| Focus loss during game | Automatic pause via `GameController` → `AppStateCheck` |
| `difficulty` exceeds config array length | `IndexOutOfRangeException` (fail-fast) |
| `difficulty` exceeds `levels` length | Returns `null` |
