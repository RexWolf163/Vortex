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
| `ReactiveValue<T>` | Abstract wrapper: `Value`, `Set(T)`, `OnUpdate`, implicit operator |
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
| `Set(T value)` | Sets the value and fires both events |
| `OnUpdate` | `event Action<T>` — typed notification |
| `OnUpdateData` | `event Action` — untyped notification (from `IReactiveData`) |
| `implicit operator T` | Read value without `.Value` |

### Guarantees
- `Set()` always fires `OnUpdate` and `OnUpdateData`, even if the value hasn't changed
- `implicit operator` allows using `ReactiveValue<T>` wherever `T` is expected
- All subclasses are constructed with an initial value: `new IntData(0)`
- `[POCO]` on `IReactiveData` makes all implementations serializable via `SerializeController`

### Constraints
- No parameterless constructor — deserialization via `FormatterServices.GetUninitializedObject()`
- `Set()` does not check equality — event on every call
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
