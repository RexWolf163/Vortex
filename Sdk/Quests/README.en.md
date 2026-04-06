# Quests

**Namespace:** `Vortex.Sdk.Quests`
**Assembly:** `ru.vortex.sdk.game.quests`

## Purpose

Quest system with asynchronous execution. Manages quest lifecycle: start condition checks, sequential logic execution, completion with result.

Capabilities:
- Lifecycle: `Unset` → `Locked` → `Ready` → `InProgress` → `Reward` → `Completed` / `Failed`
- Start conditions — AND-groups of arbitrary checks with auto-subscription (`InitListeners`/`DisposeListeners`)
- Asynchronous sequential logic execution via UniTask
- Quest autorun when conditions are met
- Recursive condition re-check on quest completion (with depth guard)
- Protection against checks in inactive game states (`GameStates.Off`, `Loading`)
- `UnFailable` mode — on failure, quest returns to `Locked` instead of `Failed`
- Cancellation of all active quests via `CancellationToken` on new game
- Quest restoration on load — skips logics up to the saved `SavePoint`

Out of scope:
- Specific quest logic (implemented in `QuestLogic` subclasses)
- Specific start conditions (implemented in `QuestConditionLogic` subclasses)
- Quest UI (only `IDataStorage` for binding)
- Specific reward implementation (implemented in `QuestRewardLogic` subclasses)

## Dependencies

### Core
- `Vortex.Core.DatabaseSystem` — `Record`, `RecordPreset`
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData` (for `SetListener`)
- `Vortex.Core.Extensions.LogicExtensions` — serialization

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `OnNewGame`

### External
- **UniTask** — async logic execution
- **Odin Inspector** — inspector attributes

## Architecture

```
QuestController (static, partial)
├── QuestModels : IGameData                       ← registered in GameModel
│   └── Dictionary<string, QuestModel> Index      ← multi-instance copies from Database
│       ├── State: QuestState (Unset→Locked→Ready→InProgress→...)
│       ├── StartConditions[]                     ← AND-groups with InitListeners/DisposeListeners
│       ├── Logics[]                              ← sequential queue
│       ├── Step: byte                             ← SavePoint key for restoration
│       ├── Autorun                               ← auto-start when Ready
│       └── UnFailable                            ← return to Locked on failure
├── ActiveQuests                                  ← Dictionary<QuestModel, UniTask>
├── CompletedQuests                               ← Dictionary<string, QuestModel>
├── Listeners                                     ← IReactiveData → auto re-check (alternative API)
└── CheckState()                                  ← subscribes to OnGameStateChanged (Reset on Off/Loading)
```

### Quest Lifecycle

```
Unset ──[NewGame/LoadGame]──→ Locked ──[conditions met]──→ Ready ──[Run()]──→ InProgress
                                ↑                            │                    │
                                │                            │ (Autorun)          ├──[all logics OK, has rewards]──→ Reward ──[GiveRewards()]──→ Completed
                                │                            │                    │
                                └────────────────[UnFailable]├──[logic Failed]    ├──[all logics OK, no rewards]──→ Completed
                                                             │                    │
                                                             └────────────────────└──[logic Failed]──→ Failed
```

`Unset` — initial state after creation from preset. On `NewGame`/`LoadGame` unconditionally transitions to `Locked`. Useful for detecting new quests on existing saves.

### Restoration on Load

On `LoadGame()`, quests in `InProgress` state are restored via `RestoreQuest`:

```
Run(quest) ──[State == InProgress]──→ RestoreQuest()
                                        ├── Step != 0 → skip logics until SavePoint with Key == Step
                                        └── Step == 0 → execute from the beginning
```

`SavePoint` is a marker logic that saves its `Key` to `QuestModel.Step` during execution. On restoration, all logics up to and including the matching `SavePoint` are skipped.

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `QuestController` | static, partial | Lifecycle controller |
| `QuestControllerExtIndex` | partial | Queries: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor integration |
| `QuestModel` | `Record` | Quest model: state, conditions, logics |
| `QuestModels` | `IGameData` | Quest index container |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject preset for Database |
| `QuestState` | `enum` | Unset, Locked, Ready, InProgress, Reward, Completed, Failed |
| `QuestLogic` | `abstract` | Atomic logic: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Save point marker: stores `Key` in `QuestModel.Step` |
| `QuestConditionLogic` | `abstract` | Condition: `Check()`, `InitListeners()`, `DisposeListeners()` |
| `QuestConditions` | `Serializable` | AND-group of conditions with subscription management |
| `QuestCompleted` | `QuestConditionLogic` | Condition: quest with given ID is complete |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | UI binding to quest by GUID |
| `RunQuestHandler` | `MonoBehaviour` | Quest launch via `IDataStorage` |

## Contract

### Input
- `QuestPreset` — ScriptableObject registered in Database as MultiInstance
- `GameController.OnNewGame` — new game trigger
- `GameController.OnLoadData` — save load trigger

### Output
- `QuestController.OnUpdateData` — change event
- `QuestController.IsComplete(id)` — completion check
- `QuestModel.OnStateUpdated` — state change event for a specific quest

### Guarantees
- Logics execute strictly sequentially
- On `NewGame()` and `LoadGame()`, all active quests are cancelled via `CancellationToken`
- `CheckQuestStartConditions` is blocked during `GameStates.Off` (calls `Reset()` on all quests) and `Loading`
- Recursive condition re-check limited to depth 10
- `UnFailable` quest on failure returns to `Locked` and does not enter `CompletedQuests` — can be restarted
- `Run()` on a quest with state `Ready` — launches `RunQuest`; with state `InProgress` — launches `RestoreQuest`; other states — error logged, call ignored
- On quest start, condition subscriptions are removed (`DisposeListeners`)

### Constraints
- Quests are strictly MultiInstance records (each game gets fresh copies)
- Single `CancellationTokenSource` for all quests — cancellation is group-wide
- `QuestConditionLogic.Check()` is synchronous, does not support async conditions

## Usage

### Creating a Quest

1. Create a `QuestLogic` subclass:
```csharp
[Serializable]
public class CollectItemsLogic : QuestLogic
{
    [SerializeField] private int targetCount;

    public override async UniTask<bool> Run(CancellationToken token)
    {
        while (Inventory.Count < targetCount)
        {
            if (token.IsCancellationRequested) return false;
            await UniTask.Yield(token);
        }
        return true;
    }
}
```

2. Create a `QuestConditionLogic` subclass (optional):
```csharp
[Serializable]
public class LevelReached : QuestConditionLogic
{
    [SerializeField] private int level;
    public override bool Check() => PlayerData.Level >= level;
}
```

3. Create a `QuestPreset` via **Assets → Create → Database → Quest Preset**
4. Configure in inspector: start conditions, logics, autorun, unFailable

### Reactive Condition Re-checks

Each `QuestConditionLogic` manages its own subscriptions via `InitListeners()`/`DisposeListeners()`:

```csharp
[Serializable]
public class NaniStarted : QuestConditionLogic
{
    public override bool Check() => NaniWrapper.IsPlaying;

    public override void InitListeners()
    {
        NaniWrapper.OnNaniStart += QuestController.CheckQuestStartConditions;
    }

    public override void DisposeListeners()
    {
        NaniWrapper.OnNaniStart -= QuestController.CheckQuestStartConditions;
    }
}
```

`QuestConditions.Check()` automatically calls `DisposeListeners` before checking and `InitListeners` only for conditions that returned `false` — subscriptions only live while the condition is unmet.

Alternative path — `SetListener`/`RemoveListener` for `IReactiveData`:

```csharp
[Serializable]
public class NaniVariableCondition : QuestConditionLogic
{
    public override bool Check() => /* ... */;

    public override void InitListeners()
    {
        QuestController.SetListener(GameController.Instance, this);
        QuestController.SetListener(NaniListener.Instance, this);
    }

    public override void DisposeListeners()
    {
        QuestController.RemoveListener(this);
    }
}
```

`SetListener` subscribes to `IReactiveData.OnUpdateData` with reference counting — one subscription per `IReactiveData` regardless of the number of conditions. `RemoveListener` unsubscribes when no sources remain.

### UI Binding

Place `QuestDataStorage` on the scene, specify the quest GUID. View components access `QuestModel` via `IDataStorage.GetData<QuestModel>()`.

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| New quest on existing save | State is `Unset`, on `LoadGame` transitions to `Locked` and participates in condition checks |
| All conditions empty | Quest immediately becomes `Ready` |
| `Autorun` + conditions met | Quest starts automatically on `NewGame`, `LoadGame`, or `CheckQuestStartConditions()` call |
| Logic returns `false`, `UnFailable = true` | State → `Locked`, not added to `CompletedQuests` (restart possible) |
| Logic returns `false`, `UnFailable = false` | State → `Failed`, quest in `CompletedQuests` |
| `NewGame()` / `LoadGame()` with active quests | All cancelled via `CancellationToken`, subscriptions removed |
| `GameStates.Off` | `CheckQuestStartConditions` calls `Reset()` on all quests, check skipped |
| `GameStates.Loading` | `CheckQuestStartConditions` skipped |
| Condition recursion > 10 levels | Interrupted (guard) |
| `Run()` on quest in `InProgress` | Restoration via `RestoreQuest` — skips logics up to `SavePoint` |
| Quest completes → another quest's conditions depend on it | Recursive re-check via `CheckQuestStartConditions` |
