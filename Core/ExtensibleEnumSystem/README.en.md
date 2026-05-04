# StateAxisSystem (Core)

**Namespace:** `Vortex.Core.StateAxisSystem.Abstractions`, `Vortex.Core.StateAxisSystem.Extensions`
**Assembly:** `ru.vortex.stateaxis`
**Platform:** .NET Standard 2.1+

---

## Purpose

Type-safe enum-like state axes, extensible per-project without modifying the framework.
An alternative to plain C# `enum` for cases where the value set is project- or domain-specific (character states, combat stances, gaze modes), but compile-time ergonomics are still required.

Capabilities:

- `StateAxis` — abstract base with auto-registration of instances via ctor
- `StateValue<T>` — reactive value of an axis (`ReactiveValue<T>` subclass)
- Serialization/deserialization via the format `"{Namespace.AxisName}.{Key}"`
- Lookup by key, index, or type via static methods on the base

Out of scope:

- Codegen of concrete axis classes — Layer 2 (`Vortex.Unity.StateAxisSystem`)
- Inspector attributes, dropdowns, paired assets — Layer 2
- UI integration (`UIStateSwitcher`) — package `Vortex.Unity.UI.StateSwitcher`

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.Extensions` | `ReactiveValue<T>`, `IReactiveData`, `SerializeController` |

The custom converter for `SerializeController` is registered automatically in the static constructor of `StateAxis` — this guarantees that as soon as any `StateAxis` subclass is loaded, the serializer already knows how to handle it.

---

## Architecture

### StateAxis

```
StateAxis (abstract, IEquatable<StateAxis>)
  ├── Key: string                                     ← stable identifier of the value
  ├── Order: int                                      ← position in axis order
  ├── ctor(string key, int order)                     ← protected, registers this in ByKey/Ordered
  ├── ToString() → "{TypeName}.{Key}"                 ← short representation for logs
  ├── Serialize() → "{FullName}.{Key}"                ← format for save/load
  ├── Equals(StateAxis other)                         ← compare by type + key
  └── static {
        ByKey:    Dictionary<Type, Dictionary<string, StateAxis>>
        Ordered:  Dictionary<Type, List<StateAxis>>
        GetByKey<T>(string)        → T
        GetByKey(Type, string)     → StateAxis
        GetAll<T>()                → IReadOnlyList<T>
        GetAll(Type)               → IReadOnlyList<StateAxis>
        GetMap(Type)               → IReadOnlyDictionary<string, StateAxis>
        Deserialize(string)        → StateAxis
        Deserialize<T>(string)     → T
      }
```

A concrete axis class is created as a `sealed` subclass with static `readonly` fields:

```csharp
public sealed class MoveState : StateAxis
{
    public static readonly MoveState Idle = new(nameof(Idle), 0);
    public static readonly MoveState Walk = new(nameof(Walk), 1);
    public static readonly MoveState Run  = new(nameof(Run),  2);

    public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

    private MoveState(string key, int order) : base(key, order) { }
}
```

On the first reference to `MoveState`:
1. The base `StateAxis` static initializer runs → registers the custom converter in `SerializeController`.
2. The `MoveState` static field initializers run → each `new MoveState(...)` invokes the base ctor → entries are added to `ByKey[typeof(MoveState)]` and `Ordered[typeof(MoveState)]`.

After that, `StateAxis.GetAll<MoveState>()` returns all four values in `Order`.

### StateValue\<T\>

```
StateValue<T> : ReactiveValue<T> where T : StateAxis
  ├── Key: string                ← Value?.Key
  ├── Index: int                 ← Value?.Order ?? -1
  ├── Is(T other)                ← ReferenceEquals(Value, other)
  └── IsKey(string key)          ← compare by key
```

`Value` holds a singleton instance of `T`. Reference equality is correct because the same value is always the same object (`MoveState.Run` is the only instance).

### StateAxisTypeCache (internal)

Lazy `FullName → Type` cache for all non-abstract `StateAxis` subclasses, populated on first access from `Deserialize`. Scans `AppDomain.CurrentDomain.GetAssemblies()`, filters by `IsAssignableFrom(typeof(StateAxis))`. Built once; not invalidated on Editor recompilation — the runtime doesn't need that, and the Editor recreates the domain.

---

## Serialization Contract

### Format

```
"{Namespace.AxisName}.{Key}"
```

Examples:
- `"MyGame.States.MoveState.Run"`
- `"Vortex.Demo.CombatState.Block"`

The separator is the **last dot** in the string. Left part — `Type.FullName` (including namespace), right — `Key`.

### Naming constraints

For unambiguous splitting:
- `AxisName` (class name) **must not contain dots** (also a C# requirement, automatically enforced).
- `Key` (value names) **must not contain dots**. The Layer 2 generator `StateAxisCodeGenerator` enforces this when saving the preset.

### Registration in SerializeController

The converter is registered once in the `StateAxis` static ctor:

```csharp
SerializeController.RegisterCustomSerializer(
    matches:     t => typeof(StateAxis).IsAssignableFrom(t),
    serialize:   obj => ((StateAxis)obj).Serialize(),
    deserialize: (t, s) => Deserialize(s)
);
```

After registration, any property whose type is a `StateAxis` subclass or `StateValue<T>.Value` is serialized as an ordinary JSON string, not expanded into an object.

---

## Usage

### Declaring an axis (manually or via codegen)

```csharp
public sealed class CombatState : StateAxis
{
    public static readonly CombatState Idle  = new(nameof(Idle),  0);
    public static readonly CombatState Block = new(nameof(Block), 1);
    public static readonly CombatState Parry = new(nameof(Parry), 2);

    public static IReadOnlyList<CombatState> All => GetAll<CombatState>();

    private CombatState(string key, int order) : base(key, order) { }
}
```

### Using in a model

```csharp
public class CharacterModel
{
    public StateValue<MoveState>   MoveMode   { get; } = new(MoveState.Idle);
    public StateValue<CombatState> CombatMode { get; } = new(CombatState.Idle);
}
```

### Read, set, check

```csharp
character.MoveMode.Set(MoveState.Run);                      // type-safe ✓
if (character.MoveMode.Value == MoveState.Run) { ... }      // reference equality
if (character.MoveMode.Is(MoveState.Run)) { ... }           // explicit
character.MoveMode.OnUpdate += v => Refresh(v);             // reactivity
int slot = character.MoveMode.Index;                        // → UIStateSwitcher
string key = character.MoveMode.Key;                        // → save without StateValue wrapper
```

### Serialization / deserialization

```csharp
var json = character.SerializeProperties();
// MoveMode field: "Value" : "MyGame.States.MoveState.Run"

var restored = json.DeserializeProperties<CharacterModel>();
restored.MoveMode.Value == MoveState.Run;                    // ✓ reference equality
```

### Lookup by key

```csharp
var run = StateAxis.GetByKey<MoveState>("Run");              // → MoveState.Run
var any = StateAxis.GetByKey(typeof(MoveState), "Walk");     // → MoveState.Walk

foreach (var s in StateAxis.GetAll<MoveState>())
    Console.WriteLine($"{s.Order}: {s.Key}");
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `GetByKey<T>("…")` for an unregistered key | Returns `null` |
| `GetAll<T>()` before any reference to `T` | Returns empty array (T's static initializer hasn't run yet) |
| `Deserialize("…")` for a missing type | Returns `null` |
| `Deserialize("…")` for an existing type but unknown key | Returns `null` |
| `Deserialize("BadString")` without a dot | Returns `null` |
| `StateValue<T>.Value == null` | `Key = null`, `Index = -1`, `Is(...) = false` |
| `StateValue<T>.Set(null)` | Allowed; full `ReactiveValue<T>` reactivity preserved |
| Duplicate Key in a subclass | The last `new` overwrites the previous in `ByKey` (incorrect usage, diagnosed by the L2 validator) |
| Concurrent threads | The registry is not thread-safe; static initializers run once in the .NET-guaranteed order, but post-init ctor calls are not protected |

---

## Public API

```csharp
// Identity + serialization
public abstract class StateAxis : IEquatable<StateAxis>
{
    public string Key { get; }
    public int Order { get; }
    
    protected StateAxis(string key, int order);
    
    public string Serialize();
    
    public static T          GetByKey<T>(string key)  where T : StateAxis;
    public static StateAxis  GetByKey(Type axisType, string key);
    public static IReadOnlyList<T>          GetAll<T>()  where T : StateAxis;
    public static IReadOnlyList<StateAxis>  GetAll(Type axisType);
    public static IReadOnlyDictionary<string, StateAxis> GetMap(Type axisType);
    
    public static StateAxis  Deserialize(string serialized);
    public static T          Deserialize<T>(string serialized) where T : StateAxis;
}

// Reactive wrapper
public class StateValue<T> : ReactiveValue<T> where T : StateAxis
{
    public StateValue();
    public StateValue(T initial);
    
    public string Key { get; }
    public int    Index { get; }
    public bool   Is(T other);
    public bool   IsKey(string key);
}
```
