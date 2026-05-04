# ExtensibleEnumSystem (Core)

**Namespace:** `Vortex.Core.ExtensibleEnumSystem.Abstractions`, `Vortex.Core.ExtensibleEnumSystem.Extensions`
**Assembly:** `ru.vortex.extenums`
**Platform:** Unity / .NET Standard 2.1+

---

## Purpose

Type-safe enum-like extensible value sets, expandable per-project without modifying the framework.
An alternative to plain C# `enum` for cases where the value set is project- or domain-specific (character states, combat stances, gaze modes), but compile-time ergonomics are still required.

Capabilities:

- `ExtensibleEnum` — abstract base with auto-registration of instances via ctor.
- `ExtEnumData<T>` — reactive value of an `ExtensibleEnum` subtype (`ReactiveValue<T>` subclass).
- Serialization/deserialization via the format `"{Namespace.TypeName}.{Key}"`.
- Eager registry initialization before the first scene loads and on Editor domain start.
- Lookup by key and by type via static methods on the base.

Out of scope:

- Codegen, paired SO assets, validators — **not provided**. Concrete classes are written by hand: `sealed class MoveState : ExtensibleEnum { … }`.
- Inspector attributes, dropdowns — Layer 2 (`Vortex.Unity.ExtensibleEnumSystem`).
- UI integration (`UIStateSwitcher`) — package `Vortex.Unity.UI.StateSwitcher`.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.Extensions` | `ReactiveValue<T>`, `IReactiveData`, `SerializeController` |
| UnityEngine | `[RuntimeInitializeOnLoadMethod]` for eager runtime initialization |
| UnityEditor (under `#if UNITY_EDITOR`) | `[InitializeOnLoadMethod]` for eager Editor-domain initialization |

The custom converter for `SerializeController` is registered automatically in the static constructor of `ExtensibleEnum` — this guarantees that as soon as the base is touched (which happens at the first `Initialize`), the serializer already knows how to handle the family.

---

## Architecture

### ExtensibleEnum

```
ExtensibleEnum (abstract, IEquatable<ExtensibleEnum>)
  ├── Key: string                                     ← stable identifier of the value
  ├── Order: int                                      ← position in logical order
  ├── ctor(string key, int order)                     ← protected, registers this in ByKey/Ordered
  ├── ToString() → "{TypeName}.{Key}"                 ← short representation for logs
  ├── Serialize() → "{FullName}.{Key}"                ← format for save/load
  ├── Equals(ExtensibleEnum other)                    ← compare by type + key
  └── static {
        ByKey:       Dictionary<Type, Dictionary<string, ExtensibleEnum>>
        Ordered:     Dictionary<Type, List<ExtensibleEnum>>
        ByFullName:  Dictionary<string, Type>          ← for Deserialize, populated by eager init
        
        [RuntimeInitializeOnLoadMethod] InitializeOnRuntime() → Initialize()
        [InitializeOnLoadMethod]        InitializeOnEditor()  → Initialize()  (#if UNITY_EDITOR)
        Initialize() — scan AppDomain + RunClassConstructor for every subclass + populate ByFullName
        
        GetByKey<T>(string)        → T
        GetByKey(Type, string)     → ExtensibleEnum
        GetAll<T>()                → IReadOnlyList<T>
        GetAll(Type)               → IReadOnlyList<ExtensibleEnum>
        GetMap(Type)               → IReadOnlyDictionary<string, ExtensibleEnum>
        Deserialize(string)        → ExtensibleEnum
        Deserialize<T>(string)     → T
      }
```

A concrete value set is created as a `sealed` subclass with static `readonly` fields:

```csharp
public sealed class MoveState : ExtensibleEnum
{
    public static readonly MoveState Idle = new(nameof(Idle), 0);
    public static readonly MoveState Walk = new(nameof(Walk), 1);
    public static readonly MoveState Run  = new(nameof(Run),  2);

    public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

    private MoveState(string key, int order) : base(key, order) { }
}
```

### Eager initialization (no lazy code)

`ExtensibleEnum.Initialize()` is called:
- at runtime — via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`;
- in the Editor domain — via `[InitializeOnLoadMethod]` under `#if UNITY_EDITOR`.

What it does:
1. Scans `AppDomain.CurrentDomain.GetAssemblies()`.
2. For every non-abstract `ExtensibleEnum` subclass:
   - records `FullName → Type` in `ByFullName` (used by `Deserialize`);
   - calls `RuntimeHelpers.RunClassConstructor(type.TypeHandle)`, which runs the subclass static initializers and populates `ByKey` / `Ordered` with registered instances.

After that `GetAll<T>()`, `GetByKey<T>()` and `Deserialize` work predictably regardless of type-load order or which code first touched a type. No lazy caches.

### ExtEnumData\<T\>

```
ExtEnumData<T> : ReactiveValue<T> where T : ExtensibleEnum
  ├── Key: string                ← Value?.Key
  ├── Index: int                 ← Value?.Order ?? -1
  ├── Is(T other)                ← ReferenceEquals(Value, other)
  └── IsKey(string key)          ← compare by key
```

`Value` holds a singleton instance of `T`. Reference equality is correct because the same value is always the same object (`MoveState.Run` is the only instance).

---

## Serialization Contract

### Format

```
"{Namespace.TypeName}.{Key}"
```

Examples:
- `"MyGame.States.MoveState.Run"`
- `"Vortex.Demo.CombatState.Block"`

The separator is the **last dot** in the string. Left part — `Type.FullName` (including namespace), right — `Key`.

### Naming constraints

For unambiguous splitting:
- The subclass type name **must not contain dots** (a C# requirement, automatically enforced).
- `Key` (value names) **must not contain dots** — author's responsibility.

### Registration in SerializeController

The converter is registered once in the `ExtensibleEnum` static ctor:

```csharp
SerializeController.RegisterCustomSerializer(
    matches:     t => typeof(ExtensibleEnum).IsAssignableFrom(t),
    serialize:   obj => ((ExtensibleEnum)obj).Serialize(),
    deserialize: (t, s) => Deserialize(s)
);
```

After registration, any property whose type is an `ExtensibleEnum` subclass or `ExtEnumData<T>.Value` is serialized as an ordinary JSON string, not expanded into an object.

---

## Usage

### Declaring a set

```csharp
public sealed class CombatState : ExtensibleEnum
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
    public ExtEnumData<MoveState>   MoveMode   { get; } = new(MoveState.Idle);
    public ExtEnumData<CombatState> CombatMode { get; } = new(CombatState.Idle);
}
```

### Read, set, check

```csharp
character.MoveMode.Set(MoveState.Run);                      // type-safe ✓
if (character.MoveMode.Value == MoveState.Run) { ... }      // reference equality
if (character.MoveMode.Is(MoveState.Run)) { ... }           // explicit
character.MoveMode.OnUpdate += v => Refresh(v);             // reactivity
int slot = character.MoveMode.Index;                        // → UIStateSwitcher
string key = character.MoveMode.Key;                        // → save without ExtEnumData wrapper
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
var run = ExtensibleEnum.GetByKey<MoveState>("Run");          // → MoveState.Run
var any = ExtensibleEnum.GetByKey(typeof(MoveState), "Walk"); // → MoveState.Walk

foreach (var s in ExtensibleEnum.GetAll<MoveState>())
    Console.WriteLine($"{s.Order}: {s.Key}");
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `GetByKey<T>("…")` for an unregistered key | Returns `null` |
| `GetAll<T>()` right after startup | Returns all values — eager init has already run |
| `Deserialize("…")` for a missing type | Returns `null` (no entry in `ByFullName`) |
| `Deserialize("…")` for an existing type but unknown key | Returns `null` |
| `Deserialize("BadString")` without a dot | Returns `null` |
| `ExtEnumData<T>.Value == null` | `Key = null`, `Index = -1`, `Is(...) = false` |
| `ExtEnumData<T>.Set(null)` | Allowed; full `ReactiveValue<T>` reactivity preserved |
| Duplicate Key in a subclass | The last `new` overwrites the previous in `ByKey` (incorrect usage by class author) |
| Pure .NET without Unity (tests) | `[RuntimeInitializeOnLoadMethod]` will not fire; call `RunClassConstructor` manually or reference at least one value before serializing |
| Concurrent threads | The registry is not thread-safe; eager init runs once before user code; post-init ctor calls are unprotected |

---

## Public API

```csharp
// Identity + serialization
public abstract class ExtensibleEnum : IEquatable<ExtensibleEnum>
{
    public string Key { get; }
    public int Order { get; }
    
    protected ExtensibleEnum(string key, int order);
    
    public string Serialize();
    
    public static T               GetByKey<T>(string key) where T : ExtensibleEnum;
    public static ExtensibleEnum  GetByKey(Type type, string key);
    public static IReadOnlyList<T>               GetAll<T>() where T : ExtensibleEnum;
    public static IReadOnlyList<ExtensibleEnum>  GetAll(Type type);
    public static IReadOnlyDictionary<string, ExtensibleEnum> GetMap(Type type);
    
    public static ExtensibleEnum  Deserialize(string serialized);
    public static T               Deserialize<T>(string serialized) where T : ExtensibleEnum;
}

// Reactive wrapper
public class ExtEnumData<T> : ReactiveValue<T> where T : ExtensibleEnum
{
    public ExtEnumData();
    public ExtEnumData(T initial);
    
    public string Key { get; }
    public int    Index { get; }
    public bool   Is(T other);
    public bool   IsKey(string key);
}
```
