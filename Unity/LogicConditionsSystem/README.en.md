# LogicConditionsSystem

**Namespace:** `Vortex.Unity.LogicConditionsSystem.Conditions`
**Assembly:** `ru.vortex.unity.logicconditions`

## Purpose

Unity implementations of conditions (`Condition`) for the logic chains system (`LogicChains`). Three ready-made conditions for common wait scenarios: timer, scene loading, application initialization.

Capabilities:
- Timer-based waiting with arbitrary delay
- Waiting for a specific scene to load
- Waiting for all systems to initialize (`AppStates.Running`)
- Abstract base `UnityCondition` for creating custom conditions

Out of scope:
- Chain orchestration (implemented in `Vortex.Core.LogicChainsSystem`)
- Domain-specific conditions (layer 3/4)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LogicChainsSystem` | `Condition` — abstract base class |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `AppStates` |
| `Vortex.Unity.AppSystem` | `TimeController.Call()`, `TimeController.RemoveCall()` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` — Inspector name display |
| Odin Inspector | `[ShowInInspector]`, `[DisplayAsString]`, `[MinValue]`, `[ValueDropdown]` |

---

## Architecture

```
LogicConditionsSystem/
└── Conditions/
    ├── _UnityCondition.cs      # Abstract base: Inspector display
    ├── MinTimeCondition.cs     # Timer-based wait
    ├── SceneLoaded.cs          # Scene loading wait
    └── SystemsLoaded.cs        # AppStates.Running wait
```

### Base Contract (Core)

`Condition` (from `Vortex.Core.LogicChainsSystem.Model`):
- `Init(Action callback)` — initialization, calls `Start()`
- `Start()` — abstract setup hook (subscriptions, initial check)
- `Check()` — current condition state
- `DeInit()` — cleanup (unsubscriptions, timers)
- `RunCallback()` — signals condition fulfillment

### UnityCondition (abstract)

Wrapper over `Condition`, adds Inspector visualization via `[ClassLabel("@ConditionName")]`. Subclasses implement `ConditionName` for display in the conditions list.

---

## Conditions

### MinTimeCondition

Waits for a specified number of seconds.

| Field | Type | Description |
|-------|------|-------------|
| `seconds` | `float` | Delay (≥ 0) |

On `Start()`, computes the target time (`DateTime.UtcNow + seconds`). Schedules a check via `TimeController.Call()` with owner binding (replaces previous call from the same owner). Fires `RunCallback()` when the target time is reached. If `seconds = 0`, triggers immediately.

### SceneLoaded

Waits for a specific scene to load.

| Field | Type | Description |
|-------|------|-------------|
| `SceneName` | `string` | Scene name (`[ValueDropdown]` from Build Settings) |

On `Start()`, checks the active scene. If the target scene is already loaded — `RunCallback()` immediately. Otherwise subscribes to `SceneManager.sceneLoaded` and waits for a name match. Unsubscribes after firing.

### SystemsLoaded

Waits for application initialization to complete.

No parameters. On `Start()`, checks `App.GetState() == AppStates.Running`. If already `Running` — `RunCallback()` immediately. Otherwise subscribes to `App.OnStateChanged`.

---

## Contract

### Input
- `Condition.Init(Action callback)` — called by the `LogicChains` system
- Configuration via serialized fields (Inspector)

### Output
- `RunCallback()` — condition fulfillment signal
- `Check()` — synchronous state query

### Guarantees
- All conditions check state in `Start()` — if already fulfilled, callback fires immediately
- `DeInit()` correctly removes all subscriptions and cancels timers

---

## Creating a Custom Condition

```csharp
public class MyCondition : UnityCondition
{
    protected override string ConditionName => "My Condition";

    protected override void Start()
    {
        if (Check())
        {
            RunCallback();
            return;
        }
        // subscribe to event...
    }

    public override bool Check() => /* check */;

    public override void DeInit()
    {
        // unsubscribe from events...
    }
}
```

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `MinTimeCondition` with `seconds = 0` | Triggers immediately in `Start()` |
| `SceneLoaded` — scene already loaded | Callback immediately, no subscription created |
| `SystemsLoaded` — app already `Running` | Callback immediately |
| `DeInit()` called before trigger | Subscriptions removed, callback not invoked |
| `MinTimeCondition` — repeated `Start()` | `TimeController` replaces previous call (owner binding) |
