# Quests

**Namespace:** `Vortex.Sdk.Quests`
**Assembly:** `ru.vortex.sdk.game.quests`

## Purpose

Quest system with asynchronous execution. Manages quest lifecycle: start condition checks, sequential logic execution, completion with result.

Capabilities:
- Lifecycle: `Locked` → `Ready` → `InProgress` → `Reward` → `Completed` / `Failed`
- Start conditions — AND-groups of arbitrary checks
- Asynchronous sequential logic execution via UniTask
- Quest autorun when conditions are met
- Recursive condition re-check on quest completion (with depth guard)
- Reactive listeners — subscribe to `IReactiveData` for automatic condition re-checks
- `UnFailable` mode — on failure, quest returns to `Locked` instead of `Failed`
- Cancellation of all active quests via `CancellationToken` on new game

Out of scope:
- Specific quest logic (implemented in `QuestLogic` subclasses)
- Specific start conditions (implemented in `QuestConditionLogic` subclasses)
- Quest UI (only `IDataStorage` for binding)
- Specific reward implementation (implemented in `QuestRewardLogic` subclasses)

## Dependencies

### Core
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `RecordPreset`
- `Vortex.Core.System.Abstractions` — `IDataStorage`, `IReactiveData`
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
│       ├── State: QuestState
│       ├── StartConditions[]                     ← AND-groups of conditions
│       ├── Logics[]                              ← sequential queue
│       ├── Autorun                               ← auto-start when Ready
│       └── UnFailable                            ← return to Locked on failure
├── ActiveQuests                                  ← Dictionary<QuestModel, UniTask>
├── CompletedQuests                               ← Dictionary<string, QuestModel>
└── Listeners                                     ← IReactiveData → auto re-check
```

### Quest Lifecycle

```
Locked ──[conditions met]──→ Ready ──[Run()]──→ InProgress
  ↑                            │                    │
  │                            │ (Autorun)          ├──[all logics OK, has rewards]──→ Reward ──[GiveRewards()]──→ Completed
  │                            │                    │
  └────────────────[UnFailable]├──[logic Failed]    ├──[all logics OK, no rewards]──→ Completed
                               │                    │
                               └────────────────────└──[logic Failed]──→ Failed
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `QuestController` | static, partial | Lifecycle controller |
| `QuestControllerExtIndex` | partial | Queries: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor integration |
| `QuestModel` | `Record` | Quest model: state, conditions, logics |
| `QuestModels` | `IGameData` | Quest index container |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject preset for Database |
| `QuestState` | `enum` | Locked, Ready, InProgress, Reward, Completed, Failed |
| `QuestLogic` | `abstract` | Atomic logic: `UniTask<bool> Run(CancellationToken)` |
| `QuestConditionLogic` | `abstract` | Condition: `bool Check()` |
| `QuestConditions` | `Serializable` | AND-group of conditions |
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
- Recursive condition re-check limited to depth 10
- `UnFailable` quest on failure returns to `Locked` and does not enter `CompletedQuests` — can be restarted
- `Run()` on a quest with state `!= Ready` — error logged, call ignored

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

```csharp
// Subscribe: on data change — automatic quest condition re-check
QuestController.SetListener(GameController.Instance, this);

// Unsubscribe
QuestController.RemoveListener(this);
```

### UI Binding

Place `QuestDataStorage` on the scene, specify the quest GUID. View components access `QuestModel` via `IDataStorage.GetData<QuestModel>()`.

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| All conditions empty | Quest immediately becomes `Ready` |
| `Autorun` + conditions met | Quest starts automatically on `NewGame`, `LoadGame`, or `CheckQuestStartConditions()` call |
| Logic returns `false`, `UnFailable = true` | State → `Locked`, not added to `CompletedQuests` (restart possible) |
| Logic returns `false`, `UnFailable = false` | State → `Failed`, quest in `CompletedQuests` |
| `NewGame()` / `LoadGame()` with active quests | All cancelled via `CancellationToken` |
| Condition recursion > 10 levels | Interrupted (guard) |
| `Run()` on quest in `InProgress` | `LogError`, duplication prevented by `ActiveQuests` check |
| Quest completes → another quest's conditions depend on it | Recursive re-check via `CheckQuestStartConditions` |
