# Quests

**Namespace:** `Vortex.Sdk.Quests`
**Assembly:** `ru.vortex.sdk.game.quests`

## Activation

The package is activated via `SdkSettings` (menu **Tools → Vortex → Configs → SDK Settings**), toggle **`questsSdk`**. The toggle controls the `USING_VORTEX_QUESTS` define symbol, listed in the asmdef's `defineConstraints` — when disabled, the package does not compile and its types are unavailable.

Activation canon for SDK packages: `Vortex/Sdk/SdkSettingsSystem/README.en.md`.

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
- Interrupt conditions (`InterruptConditions`) and the `Blocked` state — a quest is blocked from any live state (`Locked`/`Ready`/`InProgress`); the `BlockRemovable` flag sets reversibility

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
│       ├── State: QuestState (Unset→Locked→Ready→InProgress→…→Blocked)
│       ├── StartConditions[]                     ← AND-groups (AND between groups too)
│       ├── InterruptConditions[]                 ← OR-groups, prioritized over start → Blocked
│       ├── Logics[]                              ← sequential queue
│       ├── Step: byte                             ← SavePoint key for restoration
│       ├── Autorun                               ← auto-start when Ready
│       ├── UnFailable                            ← return to Locked on failure
│       └── BlockRemovable                        ← Blocked reversibility (else — permanent)
├── ActiveQuests                                  ← Dictionary<QuestModel, UniTask>
├── ActiveCts                                     ← Dictionary<QuestModel, CTS> (per-quest cancellation)
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

### Quest Interruption (`Blocked`)

A second condition set — `InterruptConditions` — answers "when to forbid" (start conditions answer "when to open"). Its fold is **mirror-opposite of start**: **OR between groups** (any group firing blocks the quest), AND within a group. An empty set ⇒ the quest is non-interruptible (backward compatibility: existing quests keep their behavior).

The interrupt check runs as the **first line** of `CheckQuestStartConditions`, before the start check — so a block always overrides an open. The quest moves to `Blocked` from any live state (`Locked`/`Ready`/`InProgress`). If it was `InProgress`, its logic is cancelled immediately (a per-quest `CancellationToken`) and progress is reset (`Step = 0`).

```
Locked / Ready / InProgress ──[any interrupt group = true]──→ Blocked
        ↑                                                        │
        └──[BlockRemovable && all interrupt groups = false]──────┘
```

**`BlockRemovable`:**
- `false` (default) — `Blocked` holds until `Reset`/new game (a permanent stop).
- `true` — once the interrupt conditions no longer hold, the quest returns to `Locked` and passes the start turnstile again. Re-entry is **strictly from scratch** (`RunQuest`, `Step = 0`); saved progress is not restored.

Terminal states (`Reward`/`Completed`/`Failed`) are not interruptible — only live ones.

> **Interrupt condition contract (INV-7).** Any re-check wakeup must go through `CheckQuestStartConditions` (direct `+=` or `SetListener`) — both start and interrupt are re-evaluated from this single point. A condition that wakes a different symbol drops out of the interrupt logic.

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `QuestController` | static, partial | Lifecycle controller |
| `QuestControllerExtIndex` | partial | Queries: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor integration |
| `QuestModel` | `Record` | Quest model: state, conditions, logics |
| `QuestModels` | `IGameData` | Quest index container |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject preset for Database |
| `QuestState` | `enum` | Unset, Locked, Ready, InProgress, Reward, Completed, Failed, Blocked |
| `QuestLogic` | `abstract` | Atomic logic: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Save point marker: stores `Key` in `QuestModel.Step` |
| `AlwaysFail` | `QuestLogic` | Hard `false`: loops an `UnFailable` quest (Locked → restart) |
| `QuestConditionLogic` | `abstract` | Condition: `Check()`, `InitListeners()`, `DisposeListeners()` |
| `QuestConditions` | `Serializable` | Condition group: `Check()` is AND. The fold between groups — AND (start) or OR (interrupt) — is at the controller level |
| `OrNotCondition` | `QuestConditionLogic` | Combinator over nested conditions with a mode (`OR` / `NOT`=NOR / `XOR`=exactly one). Subscribes to **all** children (alternativeness). Expresses non-AND logic inside an AND-group; `NOT` over a single child replaces the removed per-condition `inverted` flag |
| `QuestCompleted` | `QuestConditionLogic` | Condition: quest with given ID is complete |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | UI binding to quest by GUID |
| `RunQuestHandler` | `MonoBehaviour` | Quest launch via `IDataStorage` |

## Contract

### Input
- `QuestPreset` — ScriptableObject registered in Database as MultiInstance
- `GameController.OnNewGame` — new game trigger
- `GameController.OnLoadGame` — save load trigger

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
- On quest start, start-condition subscriptions are removed (`DisposeListeners`); interrupt subscriptions live through `Locked → Ready → InProgress`
- Interrupt is prioritized over start: the interrupt pass is the first line of `CheckQuestStartConditions`, before the start pass and on every settle-recursion iteration
- An interrupted `InProgress` quest loses progress (`Step = 0`); on a reversible block, re-entry is strictly from scratch via `RunQuest`

### Constraints
- Quests are strictly MultiInstance records (each game gets fresh copies)
- Per-quest linked CTS: cancelling one quest (on block) does not touch the others; the global `CancellationTokenSource` is for group-wide teardown (new game/load)
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

### Looped quest (`AlwaysFail`)

`AlwaysFail` is a logic returning a hard `false`. On a quest with **`unFailable = true`** it turns completion into a restart: `false` → the quest goes to `Locked` (not `Failed`), is not added to `CompletedQuests`, and a start-condition re-check launches it again.

Building a looping quest:
1. Enable **`unFailable`** on the quest.
2. Add **`AlwaysFail`** as the **last** logic (after the useful work and rewards).
3. Start conditions set the loop cadence: while they hold — the quest loops (one pass per frame; `AlwaysFail` yields a frame via `UniTask.Yield`, so the frame never hangs); a transient condition (event) makes the quest wait for the next trigger.

Logic list shape: `[…useful logics…] → [reward] → AlwaysFail`. Without `unFailable`, `AlwaysFail` simply ends the quest as `Failed`.

### Interrupt Conditions and `Blocked`

`InterruptConditions` are authored in the preset next to the start ones, but the fold between groups is **OR** (any fires → block). The condition type is the same as for start (`QuestConditionLogic`).

```csharp
// The quest is available UNTIL any of the bosses is defeated.
// interruptConditions: [ group{ BossDefeated("boss_1") },
//                        group{ BossDefeated("boss_2") } ]   // OR: any boss blocks
// blockRemovable = false                                      // permanent block
```

- Empty `interruptConditions` ⇒ the quest is non-interruptible (as before).
- Interruption fires on a running quest too: its logic is cancelled, progress is reset.
- `blockRemovable = true` — once the conditions clear, the quest returns to `Locked` and restarts from scratch.

### Reactive Condition Re-checks

Each `QuestConditionLogic` manages its own subscriptions via `InitListeners()`/`DisposeListeners()`:

```csharp
[Serializable]
public class ExampleStarted : QuestConditionLogic
{
    public override bool Check() => ExampleEngine.IsPlaying;

    public override void InitListeners()
    {
        ExampleEngine.OnStart += QuestController.CheckQuestStartConditions;
    }

    public override void DisposeListeners()
    {
        ExampleEngine.OnStart -= QuestController.CheckQuestStartConditions;
    }
}
```

`QuestConditions.Check()` automatically calls `DisposeListeners` before checking and `InitListeners` only for conditions that returned `false` — subscriptions only live while the condition is unmet.

#### Why both levels are AND (atomic deterministic tracking)

Both conditions within a group (`QuestConditions.Check` → all AND) and groups between each other (`CheckQuestStart` → `StartConditions.All`) are combined by **AND with lazy short-circuit**. This is intentional, not redundancy: at any moment the quest is subscribed to exactly **one** unmet condition — the "blocker" (first `false` in the first unmet group), and the rest are re-checked only when it opens (a re-check cascades the subscription to the next blocker). Hence:

- **atomicity** — a single active subscription at a time;
- **determinism** — the blocker is always well-defined (left to right).

> ⚠️ **Do not switch the inner level to OR.** An OR-group is true when *any* of its conditions holds, so it becomes true when *any* of them opens — you would have to subscribe to **all** conditions of an unmet group (no single blocker). That breaks atomic tracking. The organizational grouping (group name) is **not** a logical OR: the condition tree evaluates as a **flat AND**.

To express OR / NOR / XOR **inside** a group without breaking that atomicity, wrap the conditions in **`OrNotCondition`** — a single `QuestConditionLogic` that folds its children by a selectable mode (`OR` = any, `NOT` = none/NOR, `XOR` = exactly one). It deliberately subscribes to **all** its children (alternativeness — the result can change from any of them), so the group's own atomic AND-tracking stays intact; the "flat AND" invariant of the group is preserved because the OR lives *inside one* condition. Trees nest freely (an `OrNotCondition` may contain another). A `NOT`-mode `OrNotCondition` over a single child is the replacement for the removed per-condition `inverted` flag (e.g. "quest **not** complete" = `OrNotCondition{ Not, [ QuestCompleted(id) ] }`).

Alternative path — `SetListener`/`RemoveListener` for `IReactiveData`:

```csharp
[Serializable]
public class ExampleVariableCondition : QuestConditionLogic
{
    public override bool Check() => /* ... */;

    public override void InitListeners()
    {
        QuestController.SetListener(GameController.Instance, this);
        QuestController.SetListener(ExampleVariableListener.Instance, this);
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
| Empty `InterruptConditions` | Quest is non-interruptible — behaves as before the feature |
| Interrupt condition fires on `InProgress` | Logic cancelled (per-quest CTS), `Step = 0`, state → `Blocked` |
| Interrupt vs start when both true | Interrupt wins (interrupt pass runs before the start pass) |
| `BlockRemovable = false`, condition cleared | Stays `Blocked` (until `Reset`/new game) |
| `BlockRemovable = true`, condition cleared | → `Locked`; if start conditions hold — restart from scratch |
| Save/load while `Blocked` | Conditions re-read: true → stays `Blocked`; cleared and `BlockRemovable` → `Locked` |
| Interrupt in `Reward`/`Completed`/`Failed` | Does not fire — terminal states are non-interruptible |
