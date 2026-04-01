# LogicChainsSystem (Core)

**Namespace:** `Vortex.Core.LogicChainsSystem.Bus`, `Vortex.Core.LogicChainsSystem.Model`
**Assembly:** `ru.vortex.logicchains`
**Platform:** .NET Standard 2.1+

---

## Purpose

Logic chain system. Describes a sequence of steps with actions, transitions, and conditions. A chain executes step by step: entering a step triggers actions, then connectors with transition conditions activate.

Capabilities:

- Step graph with directed transitions (connectors)
- Actions (`LogicAction`) on step entry
- Conditions (`Condition`) on transitions — asynchronous, callback-based
- Automatic transitions (connector with no conditions)
- Save/load of current step via `Record`
- Chain registration by GUID, multi-instance via `Database`

Out of scope:

- Concrete actions and conditions — implemented by subclasses in Unity layer
- Visual graph editing — presets in Unity layer
- UI progress display

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemModel` |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetNewRecord()` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` for diagnostics |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` for debug logs |

---

## Architecture

```
LogicChains (static bus)
  ├── Index: Dictionary<string, LogicChain>
  ├── AddChain(LogicChain) → guid
  ├── AddChain(presetGuid) → guid         ← Database.GetNewRecord<LogicChain>
  ├── RunChain(guid)                       ← start / continue chain
  └── CheckConditions(guid, connector)     ← internal transition check

LogicChain : Record
  ├── ChainSteps: Dictionary<string, ChainStep>
  ├── StartStep: string (GUID)
  ├── CurrentStep: string (GUID)
  ├── GetDataForSave() → CurrentStep
  └── LoadFromSaveData(data) → CurrentStep = data

ChainStep : SystemModel
  ├── Guid, Name, Description
  ├── Actions: LogicAction[]              ← executed on step entry
  └── Connectors: Connector[]             ← transitions to other steps

Connector : SystemModel
  ├── TargetStepGuid: string
  └── Conditions: Condition[]             ← all must be true for transition

LogicAction (abstract)
  └── Invoke()                            ← action on step entry

Condition (abstract)
  ├── Init(Action callback)               ← start monitoring
  ├── Check() → bool                      ← check fulfillment
  ├── DeInit()                            ← stop monitoring
  ├── Start()                             ← internal initialization
  └── RunCallback()                       ← notify of possible fulfillment
```

### Chain Lifecycle

```
AddChain → RunChain → [Enter Step]
                         ├── Invoke Actions[]
                         └── For each Connector:
                              ├── Conditions.Length == 0 → auto-transition
                              └── Conditions[].Init(callback)
                                   └── callback → CheckConditions
                                        ├── All Check() == true → transition
                                        │    ├── DeInit all Conditions
                                        │    ├── CurrentStep = TargetStepGuid
                                        │    ├── target == "-1" → completion
                                        │    └── RunChain (next step)
                                        └── Any Check() == false → wait
```

### Condition — Asynchronous Model

Conditions are not polled. `Init(callback)` starts observation (event subscription, timer, etc.). When state changes, the condition calls `RunCallback()`, triggering `CheckConditions` — a check of all connector conditions. If all `Check() == true`, the transition executes.

### Chain Completion

A connector with `TargetStepGuid == "-1"` (`CompleteChainStep`) completes the chain: it is removed from `Index`. With `DebugMode` enabled, a log message is printed.

---

## Contract

### Input

- `LogicChain` added via `AddChain()` — directly or by preset GUID from `Database`
- `RunChain(guid)` starts execution

### Output

- Actions (`LogicAction.Invoke()`) called on step entry
- Transitions execute automatically when conditions are met
- Chain completes on transition to `CompleteChainStep`

### API

| Method | Description |
|--------|-------------|
| `LogicChains.AddChain(LogicChain)` | Register chain, returns GUID |
| `LogicChains.AddChain(string presetGuid)` | Create chain from Database preset, returns GUID |
| `LogicChains.RunChain(string guid)` | Start or continue chain |

### Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `CompleteChainStep` | `"-1"` | GUID marker for chain completion |

### Persistence

`LogicChain : Record` — saves and restores `CurrentStep`. On load, `RunChain` continues from the saved step.

### Constraints

| Constraint | Reason |
|------------|--------|
| Single active step per chain | `CurrentStep` is the sole pointer |
| Conditions use AND logic | All `Condition.Check()` must be `true` |
| First matching connector wins | On auto-transition (0 conditions), remaining connectors are skipped |
| No chain cancellation | Removal from `Index` only on completion |
| Cyclic chains are allowed | No infinite loop protection |

---

## Usage

### Creating an action

```csharp
public class ShowNotification : LogicAction
{
    public string Message;

    public override void Invoke()
    {
        NotificationSystem.Show(Message);
    }
}
```

### Creating a condition

```csharp
public class PlayerLevelReached : Condition
{
    public int TargetLevel;

    protected override void Start()
    {
        PlayerModel.OnLevelChanged += OnLevelChanged;
        if (Check()) RunCallback();
    }

    private void OnLevelChanged(int level)
    {
        if (Check()) RunCallback();
    }

    public override bool Check() => PlayerModel.Level >= TargetLevel;
    public override void DeInit() => PlayerModel.OnLevelChanged -= OnLevelChanged;
}
```

### Running a chain

```csharp
// From Database preset
var guid = LogicChains.AddChain("preset-guid-from-database");
LogicChains.RunChain(guid);

// From object
var chain = new LogicChain();
// ... configure ChainSteps, StartStep ...
var guid = LogicChains.AddChain(chain);
LogicChains.RunChain(guid);
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Chain GUID not found in `Index` | `Log.Print(Error)`, execution aborted |
| `StartStep` not found in `ChainSteps` | `Log.Print(Error)`, execution aborted |
| Connector with no conditions | Automatic transition to `TargetStepGuid` |
| All connector conditions met | Transition, `DeInit` all conditions of current step |
| `TargetStepGuid == "-1"` | Chain completed, removed from `Index` |
| Exception in `CheckConditions` | `Log.Print(Error)`, `condition.DeInit()` |
| `CurrentStep` already set on `RunChain` | Continues from current step (not `StartStep`) |
| `DebugMode` active | Additional log on chain completion |
