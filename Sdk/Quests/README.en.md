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
- `Vortex.Core.DatabaseSystem` — `Record`, `Database`
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData` (for `SetListener`)
- `Vortex.Core.Extensions.LogicExtensions` — serialization
- `Vortex.Core.UIProviderSystem` — `UIProvider`, `UserInterfaceData` (`CallUIOpen`, `WaitUIClose` logics)

### Unity
- `Vortex.Unity.DatabaseSystem` — `RecordPreset`, the `DbRecord` attribute
- `Vortex.Unity.AppSystem` — `TimeController` (condition re-check is deferred to the end of the frame)
- `Vortex.Unity.EditorTools` — inspector attributes (`ClassLabel`, `ClassFilter`, `DataModel`)

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `OnNewGame`, `OnLoadGame`, `OnGameStateChanged`

### External
- **UniTask** — async logic execution
- **Odin Inspector** — inspector attributes

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

`QuestConditions.Check()` checks conditions left to right: before checking each one it removes its subscriptions (`DisposeListeners`); on the first `false` it subscribes only that one (`InitListeners`) and stops — subscriptions only live while the condition is unmet.

#### Why both levels are AND (atomic deterministic tracking)

Both conditions within a group (`QuestConditions.Check` → all AND) and groups between each other (`CheckQuestStart` → `StartConditions.All`) are combined by **AND with lazy short-circuit**. This is intentional, not redundancy: at any moment the quest is subscribed to exactly **one** unmet condition — the "blocker" (first `false` in the first unmet group), and the rest are re-checked only when it opens (a re-check cascades the subscription to the next blocker). Hence:

- **atomicity** — a single active subscription at a time;
- **determinism** — the blocker is always well-defined (left to right).

> ⚠️ **Do not switch the inner level to OR.** An OR-group is true when *any* of its conditions holds, so it becomes true when *any* of them opens — you would have to subscribe to **all** conditions of an unmet group (no single blocker). That breaks atomic tracking. The organizational grouping (group name) is **not** a logical OR: the condition tree evaluates as a **flat AND**.

To express OR / NOR / XOR / nested AND **inside** a group without breaking that atomicity, wrap the conditions in **`LogicGateCondition`** — a single `QuestConditionLogic` that folds its children by a selectable mode (`OR` = any, `NOT` = none/NOR, `XOR` = exactly one, `AND` = all). It deliberately subscribes to **all** its children (alternativeness — the result can change from any of them), so the group's own atomic AND-tracking stays intact; the "flat AND" invariant of the group is preserved because the combinator lives *inside one* condition. Trees nest freely (a `LogicGateCondition` may contain another) — this is how complex trees are built, e.g. `OR` over two sets each folded by `AND`: `LogicGateCondition{ Or, [ LogicGateCondition{ And, [...] }, LogicGateCondition{ And, [...] } ] }`. A `NOT`-mode `LogicGateCondition` over a single child is the replacement for the removed per-condition `inverted` flag (e.g. "quest **not** complete" = `LogicGateCondition{ Not, [ QuestCompleted(id) ] }`). *(The class was renamed from `OrNotCondition`; existing `[SerializeReference]` references are preserved via `[MovedFrom]`.)*

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

## Architecture

```
QuestController (static, partial)
├── QuestModels : IGameData                       ← registered in GameModel
│   └── Dictionary<string, QuestModel> Index      ← multi-instance copies from Database
│       ├── QuestName: string                     ← preset name, for human reading only
│       ├── State: QuestState (Unset→Locked→Ready→InProgress→…→Blocked)
│       ├── StartConditions[]                     ← AND-groups (AND between groups too)
│       ├── InterruptConditions[]                 ← OR-groups, prioritized over start → Blocked
│       ├── Logics[]                              ← sequential queue
│       ├── Step: byte                             ← SavePoint key for restoration
│       ├── Autorun                               ← auto-start when Ready
│       ├── UnFailable                            ← return to Locked on failure
│       └── BlockRemovable                        ← Blocked reversibility (else — permanent)
├── ActiveCts                                     ← Dictionary<QuestModel, CTS>: registry of quests with running logic + per-quest cancellation
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

A quest without rewards also passes through `Reward`: after the last logic it spends one frame in `Reward`, then moves to `Completed` by itself. A quest with rewards stays in `Reward` until the game calls `GiveRewards()` (such quests are listed by `GetRewardsReady()`); `IsComplete` is already `true` for it.

### Restoration on Load

On `LoadGame()`, quests in `InProgress` state are restored via `RestoreQuest`:

```
Run(quest) ──[State == InProgress]──→ RestoreQuest()
                                        ├── Step != 0 → skip logics until SavePoint with Key == Step
                                        └── Step == 0 → execute from the beginning
```

`SavePoint` is a marker logic that saves its `Key` to `QuestModel.Step` during execution. On restoration, all logics up to and including the matching `SavePoint` are skipped. If the quest has no `SavePoint` with that key (the preset changed after the save), all logics are skipped and the quest completes at once.

### Readable name (`QuestName`)

`Record.Name` is marked `[NotPOCO]`: it comes from the preset but is neither saved nor shown in the data window. That is why the model has a separate `QuestName` field — a copy of the name of the preset with the same GUID, for human reading only. Quest logic does not rely on it.

| Moment | What happens |
|--------|--------------|
| Index build (`QuestModels`) | The name is set right away: records are created from presets, `CopyFrom` carries `Name` over, and the index copies it into `QuestName` |
| New game, load | `QuestController.FillQuestNames` sets the name again — needed for saves written before the field existed: `QuestName` is empty there and the load overwrites the value with it |

Visible in the save and in the `Tools/Vortex/SaveData/Game Index` window: without this field a quest would show up as a bare GUID.

### Quest Interruption (`Blocked`)

A second condition set — `InterruptConditions` — answers "when to forbid" (start conditions answer "when to open"). Its fold is **mirror-opposite of start**: **OR between groups** (any group firing blocks the quest), AND within a group. An empty set ⇒ the quest is non-interruptible (backward compatibility: existing quests keep their behavior).

The interrupt check runs as the **first line** of `CheckQuestStartConditions`, before the start check — so a block always overrides an open (exception — start on `LoadGame`, see Guarantees). The quest moves to `Blocked` from any live state (`Locked`/`Ready`/`InProgress`). If it was `InProgress`, its logic is cancelled immediately (a per-quest `CancellationToken`) and progress is reset (`Step = 0`).

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

### Quest Monitor (diagnostics)

Window **Tools → Vortex → Debug → Quest Monitor**, Play Mode only. Shows the quest list, the current logic, results of the last start/interrupt condition check (with the blocker), and a transition journal with reasons; provides debug actions.

Window layout:
- **Toolbar** — name search, state filter, per-state counters, journal fill (red on eviction), journal export.
- **Quest list** (left) — state, name, current logic N/M, flags `A`utorun/`U`nFailable/`B`lockRemovable, `Step`; quests with an exception in the journal are highlighted.
- **"Quest" tab** — card of the selected quest: header (name, GUID, state, flags), logics with statuses, start and interrupt condition trees with results and the blocker, rewards, the quest's journal. On the right — a fixed column: debug actions (disabled when not allowed for the current state), below a separator — "Preset" (pings the preset asset in Project).
- **"Journal" tab** — the whole journal with "selected quest only" / "errors only" filters; clicking an entry selects its quest.

- **Editor only.** Recording points (`QuestDiagnostics`) are `[Conditional("UNITY_EDITOR")]`: in a build the calls are stripped together with their arguments. Only the `TransitionReason`/`ConditionSetKind` enums (`SetState` arguments) and the `QuestDebugSettings`/`ConditionLabel` settings classes (data, no logic) remain in a build.
- **Observation without interference (INV-D1).** The monitor never calls `Check()`: condition results are recorded at the moment of the regular check, inside a context opened by the controller. Conditions not reached (short-circuit) are shown as "not evaluated". Checks outside the controller (UI switchers by conditions, etc.) do not get into the snapshots.
- **Single state-change point (INV-D2).** `QuestModel.State` changes only via `SetState(state, reason)`; each actual change is one journal entry.
- **Journal** — ring buffer (capacity in `QuestDebugSettings`, default 500), in memory only, text export by a button. There is intentionally no spam protection: a quest flooding the journal is a visible bug symptom; eviction shows in the counter and `seq` gaps.
- **Debug actions** are executed by the controller (`QuestControllerExtDebug`) through regular transitions — the invariant `Reward`/`Completed`/`Failed` ⇔ quest in `CompletedQuests` holds. "Complete with success" — Reward if there are rewards, otherwise Completed; "Complete with failure" — as a logic failure (`UnFailable` → Locked); "Reset" — from any state to Locked with `Step = 0` (quests already unlocked by it are not rolled back).
- **Condition labels** — `QuestDebugSettings.conditionLabels`: type → template, `{field}` is replaced with the serialized field value; unknown field — `{field?}`; no entry — class name.

> **Composite condition contract.** A condition that calls `Check()` on nested conditions itself (like `LogicGateCondition`) must wrap each call in `QuestDiagnostics.EnterNode(i)` / `ExitNode(result)` — otherwise its nested conditions are always "not evaluated" in the monitor.

> **Limitation.** A forced action on a quest whose logic waits for UI (`WaitUIClose`, `CallUIOpen`) cancels the logic but does not close an already opened game window.

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `QuestController` | static, partial | Lifecycle controller |
| `QuestControllerExtIndex` | partial | Queries: `IsComplete(id)`; inside the assembly — `TryGetQuest(id, out quest)` (the current game's quest by GUID; before a new game or load — `false`) |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor integration |
| `QuestControllerExtDebug` | partial, `#if UNITY_EDITOR` | Quest Monitor debug actions |
| `QuestDiagnostics` | static, internal | Monitor recording points: journal, quest trace, condition check context |
| `QuestMonitorWindow` | `EditorWindow`, `#if UNITY_EDITOR` | Quest Monitor window |
| `QuestDebugSettings` | `ScriptableObject` | Monitor settings: journal capacity, condition labels |
| `QuestModel` | `Record` | Quest model: state, conditions, logics, readable name (`QuestName`) |
| `QuestModels` | `IGameData` | Quest index container |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject preset for Database |
| `QuestState` | `enum` | Unset, Locked, Ready, InProgress, Reward, Completed, Failed, Blocked |
| `QuestLogic` | `abstract` | Atomic logic: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Save point marker: stores `Key` in `QuestModel.Step` |
| `AlwaysFail` | `QuestLogic` | Hard `false`: loops an `UnFailable` quest (Locked → restart) |
| `CallUIOpen` | `QuestLogic` | Opens a UI by ID and lets the quest proceed right away |
| `WaitUIClose` | `QuestLogic` | Waits for UIs to close: the listed ones (blacklist) or all Common except the listed ones (whitelist) |
| `QuestRewardLogic` | `abstract` | Reward: `RewardLogic()`, `GetRewardData()`; given by the controller in `GiveRewards()` |
| `QuestConditionLogic` | `abstract` | Condition: `Check()`, `InitListeners()`, `DisposeListeners()` |
| `QuestConditions` | `Serializable` | Condition group: `Check()` is AND. The fold between groups — AND (start) or OR (interrupt) — is at the controller level |
| `LogicGateCondition` | `QuestConditionLogic` | Combinator over nested conditions with a mode (`OR` / `NOT`=NOR / `XOR`=exactly one / `AND`=all). Subscribes to **all** children (alternativeness). Expresses arbitrary logic inside an AND-group; trees nest freely (e.g. `OR` over two `AND`-sets); `NOT` over a single child replaces the removed per-condition `inverted` flag. *(formerly `OrNotCondition`; serialized `[SerializeReference]` references migrate via `[MovedFrom]`)* |
| `QuestCompleted` | `QuestConditionLogic` | Condition: quest with given ID has finished with any result (`Reward`/`Completed`/`Failed`) |
| `QuestStateCondition` | `QuestConditionLogic` | Condition: the state of the quest with given ID is in the `states` whitelist (e.g. `InProgress`, or `Ready` + `InProgress`). Empty list or the quest is absent in the current game — not met. While not met, subscribes to the tracked quest's `OnStateUpdated`: starting a quest and giving rewards do not trigger a condition re-check on their own |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | UI binding to quest by GUID |
| `RunQuestHandler` | `MonoBehaviour` | Quest launch via `IDataStorage` |

## Contract

### Input
- `QuestPreset` — ScriptableObject registered in Database as MultiInstance
- `GameController.OnNewGame` — new game trigger
- `GameController.OnLoadGame` — save load trigger
- `GameController.OnGameStateChanged` — quest reset on `Off`/`Loading`
- `GameController.OnEditorGetData` (editor only) — quest initialization when data is collected outside Play Mode

### Output
- `QuestController.OnUpdateData` — change event
- `QuestController.OnCheckConditions` — a condition re-check has finished (for `QuestConditions` used outside quests)
- `QuestController.IsComplete(id)` — completion check (any result)
- `QuestController.CheckQuestStartConditions()` — request a condition re-check (runs at the end of the frame)
- `QuestModel.Run()` — start a `Ready` quest / restore an `InProgress` one
- `QuestController.GetRewardsReady()`, `QuestModel.GiveRewards()` — quests in `Reward` and giving their rewards
- `QuestModel.OnStateUpdated` — state change event for a specific quest

### Guarantees
- Logics execute strictly sequentially
- On `NewGame()` and `LoadGame()`, all active quests are cancelled via `CancellationToken`
- When the game enters `GameStates.Off` or `Loading`, the controller resets: active quests are cancelled, every quest gets `Reset()` (→ `Unset`, `Step = 0`, subscriptions removed). `CheckQuestStartConditions` does nothing in these states
- Recursive condition re-check limited to depth 10
- `UnFailable` quest on failure returns to `Locked` and does not enter `CompletedQuests` — can be restarted
- `Run()` on a quest with state `Ready` — launches `RunQuest`; with state `InProgress` — launches `RestoreQuest`; other states, or if the quest's logic is already running — error logged, call ignored
- On quest start, start-condition subscriptions are removed (`DisposeListeners`); interrupt subscriptions live through `Locked → Ready → InProgress`
- Interrupt is prioritized over start: the interrupt pass is the first line of `CheckQuestStartConditions`, before the start pass and on every settle-recursion iteration. **Exception — `LoadGame`:** `InProgress` and `Ready` + `Autorun` quests are started immediately (`Run()`), while `CheckQuestStartConditions` is deferred to the end of the frame — the synchronous part of their logics (up to the first incomplete `await`) runs before the interrupt check; if the interrupt holds, the quest is blocked at the end of the frame, side effects of already executed logics are not rolled back
- An interrupted `InProgress` quest loses progress (`Step = 0`); on a reversible block, re-entry is strictly from scratch via `RunQuest`

### Constraints
- Quests are strictly MultiInstance records (each game gets fresh copies)
- Per-quest linked CTS: cancelling one quest (on block) does not touch the others; the global `CancellationTokenSource` is for group-wide teardown (new game/load)
- `QuestConditionLogic.Check()` is synchronous, does not support async conditions

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| New quest on existing save | State is `Unset`, on `LoadGame` transitions to `Locked` and participates in condition checks |
| All conditions empty | Quest immediately becomes `Ready` |
| `Autorun` + conditions met | Quest starts automatically on `NewGame`, `LoadGame`, or `CheckQuestStartConditions()` call |
| Logic returns `false`, `UnFailable = true` | State → `Locked`, not added to `CompletedQuests` (restart possible) |
| Logic returns `false`, `UnFailable = false` | State → `Failed`, quest in `CompletedQuests` |
| `NewGame()` / `LoadGame()` with active quests | All cancelled via `CancellationToken`, subscriptions removed |
| Entering `GameStates.Off` / `Loading` | Active quests cancelled, `Reset()` on every quest → `Unset`; `CheckQuestStartConditions` is skipped in these states |
| Condition recursion > 10 levels | Interrupted (guard) |
| `Run()` on quest in `InProgress` | Restoration via `RestoreQuest` — skips logics up to `SavePoint` |
| `Run()` on a quest whose logic is already running | Error logged, repeated launch rejected |
| No `SavePoint` matches the saved `Step` | All logics skipped, the quest completes (`Reward`/`Completed`) |
| A logic throws | The exception is logged, the quest is forced → `Completed` and added to `CompletedQuests` |
| A reward throws | The exception is logged, the quest still → `Completed` |
| Quest removed from Database but present in the save | The record is removed from the index on `LoadGame` |
| Quest completes → another quest's conditions depend on it | Recursive re-check via `CheckQuestStartConditions` |
| Empty `InterruptConditions` | Quest is non-interruptible — behaves as before the feature |
| Interrupt condition fires on `InProgress` | Logic cancelled (per-quest CTS), `Step = 0`, state → `Blocked` |
| Interrupt vs start when both true | Interrupt wins (interrupt pass runs before the start pass) |
| `LoadGame`: `InProgress` or `Ready` + `Autorun` quest with interrupt holding | The quest is started first (the synchronous part of logics runs), blocked at the end of the frame with its logic cancelled; side effects are not rolled back |
| `BlockRemovable = false`, condition cleared | Stays `Blocked` (until `Reset`/new game) |
| `BlockRemovable = true`, condition cleared | → `Locked`; if start conditions hold — restart from scratch |
| Save/load while `Blocked` | Conditions re-read: true → stays `Blocked`; cleared and `BlockRemovable` → `Locked` |
| Interrupt in `Reward`/`Completed`/`Failed` | Does not fire — terminal states are non-interruptible |
