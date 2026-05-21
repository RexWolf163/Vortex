# ReactiveValues

**Namespace:** `Vortex.Core.Extensions.ReactiveValues`
**Assembly:** `ru.vortex.extensions`
**Platform:** .NET Standard 2.1+

---

## Purpose

Reactive wrappers over simple data types. Notify subscribers when the value changes.

Capabilities:
- Typed `OnUpdate` event with the new value
- Untyped `OnUpdateData` event (`IReactiveData` interface)
- Implicit operator for reading without `.Value`
- Container ownership — only the owner can modify the value via `Set()`
- `IReactiveData` interface is marked `[POCO]` — all implementations are automatically serializable via `SerializeController`

Out of scope:
- Thread safety
- Value validation
- Thread-safe subscription/unsubscription

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.Extensions.LogicExtensions.SerializationSystem` | `[POCO]` attribute on `IReactiveData` |
| `Vortex.Core.LoggerSystem` | `Log.Print` for ownership errors |

---

## Architecture

```
IReactiveData [POCO]                     ← interface: event OnUpdateData
├── ReactiveValue<T> (abstract)          ← single value
│   ├── IntData                          ← ReactiveValue<int>
│   ├── FloatData                        ← ReactiveValue<float>
│   ├── BoolData                         ← ReactiveValue<bool>
│   ├── StringData                       ← ReactiveValue<string>, ToString()
│   └── EnumData<TEnum>                  ← ReactiveValue<TEnum> where TEnum : Enum
│
└── ReactiveCollection<T> (abstract)     ← reactive List<T>: Add/Remove/Insert/Sort/...
    └── ListData<T>                      ← constructors (empty / from List<T>)
```

### Components

| Class | Purpose |
|-------|---------|
| `IReactiveData` | Interface with `event Action OnUpdateData`. Marked `[POCO]` |
| `ReactiveValue<T>` | Single-value wrapper: `Value`, `Set(T, owner)`, `SetOwner()`, `ForceUpdate()`, `OnUpdate`, implicit operator |
| `IntData` | `ReactiveValue<int>`. Constructors: `(int)`, `(int, object owner)` |
| `FloatData` | `ReactiveValue<float>`. Constructors: `(float)`, `(float, object owner)` |
| `BoolData` | `ReactiveValue<bool>`. Constructors: `(bool)`, `(bool, object owner)` |
| `StringData` | `ReactiveValue<string>`, `ToString()`. Constructors: `(string)`, `(string, object owner)` |
| `EnumData<TEnum>` | `ReactiveValue<TEnum>` for any `Enum`. Constructors: `(TEnum)`, `(TEnum, object owner)` |
| `ReactiveCollection<T>` | Abstract reactive `List<T>`: `OnUpdate(IReadOnlyList<T>)`, mutators Add/Remove/Insert/Sort/Reverse/Clear/RemoveAt/RemoveRange/Set(index, …)/Set(List<T>), `GetList()`, read-only indexer, `SetOwner`/`ReleaseOwner`/`ForceUpdate`. Internal list identity is preserved for the lifetime of the container. No constructors — use `ListData<T>` |
| `ListData<T>` | Canonical `ReactiveCollection<T>` subclass with initializing constructors: `()` empty, `(List<T>)` copy of supplied |

---

## Contract

### API

| Method / Property | Description |
|-------------------|-------------|
| `Value` | Current value (public get, protected set) |
| `Set(T value, object owner = null)` | Sets the value. Ignored if the value hasn't changed. If an owner is assigned, only the owner can modify the value |
| `SetOwner(object owner)` | Assign container owner. Reassignment is not allowed |
| `ForceUpdate()` | Force-fires `OnUpdate` and `OnUpdateData` without changing the value |
| `OnUpdate` | `event Action<T>` — typed notification |
| `OnUpdateData` | `event Action` — untyped notification (from `IReactiveData`) |
| `implicit operator T` | Read value without `.Value` |

### Guarantees
- `Set()` fires events only when the value actually changes (deduplication via `EqualityComparer<T>.Default`)
- `Set()` with a wrong owner logs an error and does not change the value
- `SetOwner()` prevents reassignment — logs an error on repeated calls
- Without an owner (`_owner == null`), `Set()` works without restrictions
- `ForceUpdate()` fires events without checking for value change
- `implicit operator` allows using `ReactiveValue<T>` wherever `T` is expected
- All subclasses can be constructed with an initial value: `new IntData(0)` or with an owner: `new IntData(0, owner)`
- `[POCO]` on `IReactiveData` makes all implementations serializable via `SerializeController`

### Constraints
- No parameterless constructor — deserialization via `FormatterServices.GetUninitializedObject()`
- Owner is assigned once and cannot be released
- Not thread-safe

---

## Usage

### Declaration

```csharp
public class PlayerModel
{
    public IntData Level { get; set; } = new IntData(1);
    public StringData Name { get; set; } = new StringData("Player");
    public BoolData IsAlive { get; set; } = new BoolData(true);
}
```

### Subscribing to changes

```csharp
var model = new PlayerModel();

// Typed subscription
model.Level.OnUpdate += newLevel => Debug.Log($"Level: {newLevel}");

// Untyped subscription (IReactiveData)
model.Level.OnUpdateData += () => Debug.Log("Level changed");
```

### Implicit operator

```csharp
int level = model.Level;           // implicit operator
string name = model.Name;          // implicit operator
if (model.IsAlive) { /* ... */ }   // implicit operator
```

### Changing value

```csharp
model.Level.Set(5);   // fires OnUpdate(5) and OnUpdateData
model.Level.Set(5);   // repeated call — value unchanged, events are NOT fired
```

### Constructor with owner

```csharp
// Container with owner assigned at creation
var hp = new IntData(100, this);
hp.Set(90, this);    // OK
hp.Set(90, other);   // Error
```

### Forced update

```csharp
// Fire events without changing the value
model.Level.ForceUpdate();
```

### Container ownership

```csharp
// Controller assigns itself as owner
model.Level.SetOwner(this);

// Only the owner can modify the value
model.Level.Set(10, this);    // OK
model.Level.Set(10, other);   // Error: "Trying to change value from outer Object."
model.Level.Set(10);          // Error: owner = null != this
```

### Locking with a private key (recommended)

Passing `this` as the owner works, but it **leaks**: the controller reference is usually publicly accessible, and any code can call `data.Set(value, controller)` pretending to be the owner. The lock loses its purpose.

The clean pattern — a dedicated private key inside the controller:

```csharp
public class HealthController
{
    private readonly object _key = new();
    public IntData Hp { get; } = new IntData(100);

    public HealthController()
    {
        Hp.SetOwner(_key);
    }

    public void Damage(int amount)
    {
        Hp.Set(Hp - amount, _key);   // OK
    }
}

// From outside:
controller.Hp.Set(0, controller);  // Error — `controller` is not the key
controller.Hp.Set(0);              // Error — null is not the key
```

`_key` is `private`, invisible from outside, instantiated inside the controller and never published. Neither reflection (by `object` type) nor an accidental reference leak gives outsiders a way around the lock.

The same approach works for `ReactiveCollection<T>` (`ListData<T>` etc.) and for owner-based constructors:

```csharp
public IntData Hp { get; }

public HealthController()
{
    Hp = new IntData(100, _key);   // owner is the key from the start
}
```

### Reactive collection

```csharp
public class InventoryModel
{
    public ListData<ItemId> Items { get; private set; } = new();
}

inventory.Items.OnUpdate += list => RefreshUI(list);   // IReadOnlyList<ItemId>
inventory.Items.OnUpdateData += () => Counter.Refresh(); // untyped notification

inventory.Items.Add(itemId);                 // OnUpdate fires
inventory.Items.Remove(missingId);           // not found → OnUpdate does NOT fire
inventory.Items.SetOwner(this);              // from now on only this can mutate
inventory.Items.Add(itemId, this);           // OK
inventory.Items.Add(itemId, other);          // Error: foreign owner

var snapshot = inventory.Items.GetList();    // IReadOnlyList<ItemId>, read-only access
```

### Usage with QuestController

```csharp
// IReactiveData allows subscribing to changes for quest condition re-checks
QuestController.SetListener(model.Level, this);
```

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `Set()` with the same value | Ignored, events are not fired |
| `ForceUpdate()` | Fires `OnUpdate` and `OnUpdateData` with the current value |
| `implicit operator` on null | NRE — `ReactiveValue` is not nullable |
| Deserialization without constructor | Fallback to `FormatterServices.GetUninitializedObject()` |
| `[POCO]` on `IReactiveData` | All `ReactiveValue<T>` subclasses are serializable automatically |
| `Set()` without owner when `_owner` is set | Error — `owner = null` does not equal `_owner` |
| `SetOwner(null)` | Ignored (early return) |
| Repeated `SetOwner()` | Error, owner is not reassigned |
| Instantiating `ReactiveCollection<T>` directly | Not possible — the class is abstract. Use the `ListData<T>` subclass |
| `ReactiveCollection.Remove(v)` for a missing element | Events are not fired (no change happened) |
| `ReactiveCollection.Set(index, value)` with the same value | No deduplication — the event fires anyway (unlike `ReactiveValue<T>.Set`) |
| `ReactiveCollection.Set(index, value)` / `RemoveAt(index)` / `RemoveRange(...)` / `Insert(...)` with invalid index | `ArgumentOutOfRangeException` / `ArgumentException` (standard `List<T>` behavior). Events are not fired |
| `ReactiveCollection.Sort()` for `T` without `IComparable<T>` | `InvalidOperationException`. No overload with comparator available |
| `ReactiveCollection.SetOwner` called twice | Error, owner is not reassigned. Call `ReleaseOwner(currentOwner)` first |
| `ReactiveCollection.ReleaseOwner` with the wrong key | Error, owner is not cleared |
| `ReactiveCollection.GetList()` | Returns a `ReadOnlyCollection<T>` view over the internal list. **Live** snapshot — reflects all subsequent mutations (Add/Remove/Insert/Sort/...) and full replacement via `Set(List<T>)` (which uses Clear+AddRange and preserves the internal list's identity). Cannot be modified directly |
| `new ListData<T>(list)` | Stores a copy of the supplied list (via `.ToList()`); mutations of the source do not affect the container |
| `new ListData<T>(null)` | `NullReferenceException`. Use the parameterless constructor for an empty list |
