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
- Duplicate suppression (event fires on every `Set`, even if the value hasn't changed)

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
└── ReactiveValue<T> (abstract)          ← Value, Set(), OnUpdate, OnUpdateData, implicit operator T
    ├── IntData                          ← ReactiveValue<int>
    ├── FloatData                        ← ReactiveValue<float>
    ├── BoolData                         ← ReactiveValue<bool>
    └── StringData                       ← ReactiveValue<string>, ToString()
```

### Components

| Class | Purpose |
|-------|---------|
| `IReactiveData` | Interface with `event Action OnUpdateData`. Marked `[POCO]` |
| `ReactiveValue<T>` | Abstract wrapper: `Value`, `Set(T, owner)`, `SetOwner()`, `OnUpdate`, implicit operator |
| `IntData` | `ReactiveValue<int>` |
| `FloatData` | `ReactiveValue<float>` |
| `BoolData` | `ReactiveValue<bool>` |
| `StringData` | `ReactiveValue<string>`, overrides `ToString()` |

---

## Contract

### API

| Method / Property | Description |
|-------------------|-------------|
| `Value` | Current value (public get, protected set) |
| `Set(T value, object owner = null)` | Sets the value and fires both events. If an owner is assigned, only the owner can modify the value |
| `SetOwner(object owner)` | Assign container owner. Reassignment is not allowed |
| `OnUpdate` | `event Action<T>` — typed notification |
| `OnUpdateData` | `event Action` — untyped notification (from `IReactiveData`) |
| `implicit operator T` | Read value without `.Value` |

### Guarantees
- `Set()` always fires `OnUpdate` and `OnUpdateData`, even if the value hasn't changed
- `Set()` with a wrong owner logs an error and does not change the value
- `SetOwner()` prevents reassignment — logs an error on repeated calls
- Without an owner (`_owner == null`), `Set()` works without restrictions
- `implicit operator` allows using `ReactiveValue<T>` wherever `T` is expected
- All subclasses are constructed with an initial value: `new IntData(0)`
- `[POCO]` on `IReactiveData` makes all implementations serializable via `SerializeController`

### Constraints
- No parameterless constructor — deserialization via `FormatterServices.GetUninitializedObject()`
- `Set()` does not check equality — event on every call
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

### Usage with QuestController

```csharp
// IReactiveData allows subscribing to changes for quest condition re-checks
QuestController.SetListener(model.Level, this);
```

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `Set()` with the same value | Event fires |
| `implicit operator` on null | NRE — `ReactiveValue` is not nullable |
| Deserialization without constructor | Fallback to `FormatterServices.GetUninitializedObject()` |
| `[POCO]` on `IReactiveData` | All `ReactiveValue<T>` subclasses are serializable automatically |
| `Set()` without owner when `_owner` is set | Error — `owner = null` does not equal `_owner` |
| `SetOwner(null)` | Ignored (early return) |
| Repeated `SetOwner()` | Error, owner is not reassigned |
