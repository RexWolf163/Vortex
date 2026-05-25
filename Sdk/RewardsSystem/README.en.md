# RewardsSystem

Vortex Sdk package for configurable reward distribution: preset → weighted pack roll → per-item dispatch through polymorphic strategies → bus events.

## Purpose

- Reward configuration as a ScriptableObject preset with weighted packs
- Polymorphic dispatch strategies (`[SerializeReference]` dropdown in the inspector) for domain rules — inventory, currency, progress
- Unified result contract (`RewardResult`) with a reward type (`RewardType : ExtensibleEnum`) for downstream filtering
- Event bus `RewardBus` for UI reactions (floating text, achievements, save markers) without direct coupling

Out of scope:
- Concrete dispatch rules (inventory, wallet, loot tables) — that belongs to domain packages implementing `RewardStrategy` subclasses
- Batch validation / batch dispatch as a single transaction — depends on the receiving system (see below)
- Reward presentation in UI — subscribe to `RewardBus`, presentation is the consumer's job

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.system` | base abstractions (asmdef reference) |
| `ru.vortex.extensions` | `DeepCopy`, `ActionExt` |
| `ru.vortex.extenums` | `ExtensibleEnum` for `RewardType` |
| Sirenix Odin Inspector | `[OnInspectorInit]`, `[OnValueChanged]`, `[HideReferenceObjectPicker]`, `[ShowInInspector]`, `[HideLabel]`, `[HorizontalGroup]` |
| UnityEngine | `ScriptableObject`, `Random`, `Debug.LogException` |

Assembly: `ru.vortex.sdk.game.rewards`. No constraints — compiled unconditionally.

## Architecture

```
RewardPreset (ScriptableObject)               ← designer config
   └─ RewardPack[]                            ← weighted groups
         └─ RewardData[]                      ← named entry
                └─ RewardStrategy             ← polymorphic dispatch
                       │
                       │  (domain Sdk/project extension)
                       ▼
                  RewardStrategy.Type → RewardType (ExtensibleEnum, partial)

Dispatch flow:
   preset.GetReward()  →  IReadOnlyList<RewardData>  (DeepCopy of the picked pack)
   reward.GiveReward() →  RewardResult { Success, FailReason, AppliedAmount, Type }
                          ↓
                       RewardBus.OnRewardGiven / OnRewardFailed
                          ↓
                       subscribers: UI, achievements, save, analytics
```

The source of truth for the reward type is `RewardStrategy.Type` (abstract property). `RewardResult.Type` is a snapshot of that value, filled in automatically by `RewardsExtLogic.GiveReward`, so consumers can filter a batch of results without back-referencing the original `RewardData` (the strategy is `internal`).

## Key concepts

| Concept | Description | Example |
|---------|-------------|---------|
| Preset | ScriptableObject with N packs and their weights | `Assets → Create → Database → Reward Preset` |
| Pack (`RewardPack`) | A group dispatched as a whole when picked | Weight 30, contains [«Sword», «50 coins»] |
| Reward (`RewardData`) | Name + polymorphic strategy | `[SerializeReference] RewardStrategy` |
| Strategy (`RewardStrategy`) | Concrete dispatch rule: validation + model mutation | `GiveItemReward : RewardStrategy` |
| Reward type (`RewardType`) | `ExtensibleEnum` for grouping/filtering | `RewardType.Item`, `RewardType.Currency` |
| Result (`RewardResult`) | `struct { Success, FailReason, AppliedAmount, Type }` | Returned from `GiveReward` |
| Bus (`RewardBus`) | Events `OnRewardGiven` / `OnRewardFailed` | UI / achievement subscriptions |

## Critical requirements

1. **Every strategy must declare `Type`.** `RewardStrategy.Type` is abstract; the compiler will not let you skip it. Without `Type`, result filtering loses its purpose.
2. **Reward types are declared in the strategy's package, not in this package.** `RewardType` is a `partial class` with no built-in values. Each domain extension (Sdk package for inventory, wallet, progression) adds its own values via a partial extension.
3. **`GetReward` may return `null`.** If the sum of all pack weights is 0, the preset is considered invalid and the roll is cancelled. Consumers must check the result.
4. **Strategies must not write to `RewardBus`.** Event emission is `internal` and only `RewardsExtLogic.GiveReward` performs it. The strategy mutates the model; the bus reports the established fact.
5. **A direct call to `RewardStrategy.GiveReward()` leaves `RewardResult.Type == null`.** Filling in the type is the extension's responsibility. Strategies are not required to remember `Type` in each return.

## Contract

### Input

- `RewardPreset` asset with configured `RewardPack[]` and weights
- For every `RewardData` — a concrete `RewardStrategy` chosen in the inspector via the `[SerializeReference]` dropdown
- Optional for `GiveReward`/`ValidateRewardConditions`: `targetId` (recipient ID) and `power` (integer power multiplier)

### Output

- `preset.GetReward()` → `IReadOnlyList<RewardData>` or `null` (see edge cases)
- `reward.GiveReward()` → `RewardResult { Success, FailReason, AppliedAmount, Type }`
- `reward.ValidateRewardConditions()` → `bool` without side effects
- Events `RewardBus.OnRewardGiven` / `OnRewardFailed` — after the actual model mutation or failure

### Guarantees

- `GetReward` returns a DeepCopy of the chosen pack. Mutating the result does not touch the source preset asset.
- On a `GiveReward` call exactly one event is emitted — either `OnRewardGiven` or `OnRewardFailed`. Never both.
- A strategy exception is caught, logged via `Debug.LogException`, `OnRewardFailed` is emitted, and `RewardResult.Fail("Logic error")` is returned. The bus payload and the return value carry the same `Result`.
- `RewardResult.Type` is filled in automatically by `RewardsExtLogic.GiveReward` for all three branches (validation fail, normal result, exception).

### Limitations

- **Synchronous dispatch only.** `GiveReward` returns `RewardResult`, not `UniTask<RewardResult>`. Long-running operations (network commit, async animation) live outside the strategy, in bus subscribers.
- **Rewards are discrete.** `AppliedAmount` is `int`. `power` is an integer multiplier over dispatch steps, not a fractional reward size. The strategy is responsible for rounding/truncating any remainder.
- **Batch `GiveAll` is deliberately absent.** Correct batch dispatch requires knowledge of the receiving model (an inventory with one free slot and two item rewards — each valid individually, not together). That check belongs to the receiving system, not to a generic reward bus.
- **`RewardData.RewardStrategy` is `internal`.** The strategy is not reachable from outside the assembly; all work goes through `RewardsExtLogic` extension methods.

## API Reference

```csharp
// Pick a reward group from the preset (DeepCopy, no side effects)
IReadOnlyList<RewardData> GetReward(this RewardPreset preset);

// Check a single reward without side effects (for UI previews)
bool ValidateRewardConditions(this RewardData reward, string targetId = null, float power = 1f);

// Synchronous dispatch of a single reward (model mutation + bus event)
RewardResult GiveReward(this RewardData reward, string targetId = null, float power = 1f);

// Bus events
event Action<RewardEventData> RewardBus.OnRewardGiven;
event Action<RewardEventData> RewardBus.OnRewardFailed;

// Extending the reward-type registry (in a domain package)
public partial class RewardType
{
    public static readonly RewardType Item     = new(nameof(Item), 100);
    public static readonly RewardType Currency = new(nameof(Currency), 110);
}

// A new strategy (in a domain package)
public class GiveItemReward : RewardStrategy
{
    [SerializeField] private string itemId;
    [SerializeField] private int amount = 1;

    public override string   GetLabel() => $"Item {itemId} x{amount}";
    public override RewardType Type     => RewardType.Item;

    public override bool Validation(string targetId, float power)
        => Inventory.HasSlotFor(targetId, itemId, (int)(amount * power));

    public override RewardResult GiveReward(string targetId, float power)
    {
        var applied = Inventory.Give(targetId, itemId, (int)(amount * power));
        return applied > 0 ? RewardResult.Ok(applied) : RewardResult.Fail("InventoryFull");
    }
}
```

## Usage

### 1. Basic scenario (one preset, one pack rolled)

```csharp
[SerializeField] private RewardPreset chestPreset;

public void OpenChest(string playerId)
{
    var rewards = chestPreset.GetReward();
    if (rewards == null) return;                    // all weights are zero

    foreach (var reward in rewards)
        reward.GiveReward(targetId: playerId);
}
```

> 💡 **Note:** `foreach + GiveReward` is acceptable when cumulative effects are known to be absent (currency, points, progress flags). For items going into an inventory with a limited slot count, do a pre-check on the receiving system — see scenario 3.

### 2. UI preview for reward availability

```csharp
foreach (var reward in chestPreset.RewardPacks[0].Rewards)
{
    var canGive = reward.ValidateRewardConditions(playerId);
    button.interactable = canGive;
}
```

### 3. Batch dispatch with a pre-check on the receiving system

```csharp
var rewards = chestPreset.GetReward();
if (rewards == null) return;

// Group by type — each type is validated by its own system
var items    = rewards.Where(r => r.GetType() == typeof(GiveItemReward));
var currency = rewards.Where(r => r.GetType() == typeof(GiveCurrencyReward));

// The receiving system checks the cumulative effect
if (!Inventory.CanFitAll(playerId, items))
{
    UI.ShowOverflowDialog();
    return;
}

foreach (var reward in rewards)
    reward.GiveReward(playerId);
```

After dispatch, filter results by `RewardResult.Type`:

```csharp
var results = rewards.Select(r => r.GiveReward(playerId)).ToList();
var itemsApplied = results.Where(r => r.Type == RewardType.Item).Sum(r => r.AppliedAmount);
```

### 4. UI reaction via the bus

```csharp
private void OnEnable()  => RewardBus.OnRewardGiven += ShowFloatingText;
private void OnDisable() => RewardBus.OnRewardGiven -= ShowFloatingText;

private void ShowFloatingText(RewardEventData data)
{
    if (data.Result.Type == RewardType.Currency)
        floatingTextPool.Spawn($"+{data.Result.AppliedAmount}");
}
```

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `preset.RewardPacks` is null or empty | `GetReward` throws `NullReferenceException` (fail-fast: broken config) |
| Single pack in the preset | Always returned; its `Weight` is ignored |
| Sum of all pack weights equals 0 | `GetReward` returns `null` |
| `strategy.Validation` returned `false` | `GiveReward` → `RewardResult.Fail("ValidationFailed")` + `OnRewardFailed` |
| `strategy.GiveReward` threw an exception | `Debug.LogException` + `OnRewardFailed` + return `RewardResult.Fail("Logic error")` |
| Direct call to `RewardStrategy.GiveReward()` bypassing the extension | `RewardResult.Type == null`, bus is not notified |
| `targetId == null` | Global reward — the strategy decides whether it is supported |

## File layout

```
RewardsSystem/
├── RewardBus.cs                # static bus OnRewardGiven/OnRewardFailed + RewardEventData
├── RewardPreset.cs             # ScriptableObject config
├── RewardsExtLogic.cs          # extension methods GetReward/Validate/GiveReward
├── Model/
│   ├── RewardData.cs           # name + [SerializeReference] strategy
│   ├── RewardPack.cs           # weight + RewardData[]
│   ├── RewardResult.cs         # struct { Success, FailReason, AppliedAmount, Type }
│   ├── RewardStrategy.cs       # abstract: GetLabel, Type, Validation, GiveReward
│   └── RewardType.cs           # partial ExtensibleEnum for domain types
└── ru.vortex.sdk.game.rewards.asmdef
```
