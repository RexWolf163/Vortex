# ComplexModelSystem

Base class for composite data models whose structure is determined by loaded packages.

## Purpose

Automatic model assembly from all concrete implementations of an interface/base class `T` found in loaded assemblies. Serialization and deserialization via `SerializeController`.

- Scans `AppDomain.CurrentDomain.GetAssemblies()` for `T` implementations
- Indexes instances by type: `Dictionary<Type, T>`
- Typed component access: `Get<TU>()`
- Type caching (per `T`) — subsequent `Init()` calls skip assembly scanning
- Serialization/deserialization via `SerializeController` (property-based JSON)

Out of scope: parameterized constructors, Database registration, Unity-specific logic.

## Dependencies

- `Vortex.Core.Extensions.LogicExtensions.SerializationSystem` — `SerializeProperties()`, `DeserializeProperties<T>()`
- `Vortex.Core.LoggerSystem` — error logging

## Architecture

```
ComplexModel<T> (abstract, Serializable)
├── Cache          — static Dictionary<Type, Type[]> (type cache per T)
├── Index          — Dictionary<Type, T> (component instances)
├── Init()         — assembly scanning or cache restoration
├── Get<TU>()      — typed component access
├── Serialize()    — → JSON (calls BeforeSerialization/AfterSerialization)
├── Deserialize()  — ← JSON (calls BeforeDeserialization/AfterDeserialization)
└── abstract hooks — BeforeSerialization, AfterSerialization, BeforeDeserialization, AfterDeserialization
```

### Init()

1. Clears `Index`
2. If `Cache` contains types for `T` — creates instances from cache (`Activator.CreateInstance`)
3. Otherwise — scans all assemblies: finds non-abstract, non-interface types assignable from `T` with a parameterless constructor
4. Stores discovered types in `Cache[typeof(T)]`

### Requirements for T

- Concrete implementations must have a parameterless constructor
- Not abstract, not interface
- `class` constraint

## Contract

### Input
- `Init()` call for scanning and instance creation
- `Deserialize(string)` for restoration from JSON

### Output
- `Get<TU>()` — typed component access
- `Serialize()` — JSON string via `SerializeController`

### Guarantees
- Type cache — subsequent `Init()` calls skip assembly scanning (for the same `T`)
- `Get<TU>()` on missing type — `null` + `Error` log
- `Deserialize(null/empty)` — `Error` log, index unchanged
- Exceptions during assembly scanning are caught — `Warning` log

### Limitations
- One instance per type — duplicate types are not possible
- `Activator.CreateInstance` — parameterless constructors only
- Cache is static per `T` — shared across all `ComplexModel<T>` instances
- Serialization via `SerializeController` (experimental)

## Usage

### Model definition

```csharp
public interface IPlayerData { }

public class HealthData : IPlayerData
{
    public int Hp { get; set; } = 100;
}

public class InventoryData : IPlayerData
{
    public List<string> Items { get; set; } = new();
}

public class PlayerModel : ComplexModel<IPlayerData>
{
    protected override void BeforeSerialization() { }
    protected override void AfterSerialization() { }
    protected override void BeforeDeserialization() { }
    protected override void AfterDeserialization() { }
}
```

### Initialization and access

```csharp
var model = new PlayerModel();
model.Init(); // discovers HealthData, InventoryData

var health = model.Get<HealthData>();
health.Hp -= 10;

var inventory = model.Get<InventoryData>();
inventory.Items.Add("sword");
```

### Serialization

```csharp
string json = model.Serialize();

var restored = new PlayerModel();
restored.Init();
restored.Deserialize(json);
```

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `Get<TU>()` with unregistered type | `null` + `Error` log |
| `Init()` with no `T` implementations | Empty `Index`, works without errors |
| Repeated `Init()` | `Index` cleared and recreated from cache |
| Assembly throws on `GetTypes()` | Caught, `Warning` log, scanning continues |
| `Deserialize("")` / `Deserialize(null)` | `Error` log, `Index` unchanged |
| Two different `ComplexModel<T>` with same `T` | Shared `Cache`, independent `Index` |
