# MappedParametersSystem (Core)

**Namespace:** `Vortex.Core.MappedParametersSystem.Bus`, `Vortex.Core.MappedParametersSystem.Base`
**Assembly:** `ru.vortex.core.mappedparameters`
**Platform:** .NET Standard 2.1+

---

## Purpose

Parametric map system. Describes a dependency graph between named parameters with directed links and costs. A map defines the structure (which parameters exist and how they are connected), a model stores concrete values.

Capabilities:

- Parameter graph: base (root) and derived (with parents)
- Links (`IParameterLink`) with numeric cost (`Cost`)
- Cost aggregation logic for multiple parents (`And`, `Or`, `Sum`)
- `GenericParameter[]` array retrieval for runtime use
- `IMappedModel` creation and initialization via `ParameterMaps` bus
- Map GUID — `FullName` of the model type

Out of scope:

- Map storage (ScriptableObject presets) — Unity layer
- Inspector editing, validation, attributes — Unity layer
- Cost interpretation — application-level controller

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `Singleton<T>` |
| `Vortex.Core.Extensions` | `StringExtensions`, `DictionaryExt.AddNew()` |
| `Vortex.Core.Extensions.LogicExtensions` | `ActionExt.Fire()` |

---

## Architecture

```
ParameterMaps : SystemController<ParameterMaps, IDriverMappedParameters>
  ├── _parametersMaps: Dictionary<string, ParametersMap>
  ├── GetParameters<T>() → GenericParameter[]
  ├── GetParameters(Type) → GenericParameter[]
  ├── GetParameters(string fullName) → GenericParameter[]
  ├── GetModel<T>() → IMappedModel          ← Activator.CreateInstance + Init
  ├── GetModel(Type) → IMappedModel
  └── InitMap(IMappedModel) → void           ← initialize existing instance

ParametersMap
  ├── Guid: string                           ← FullName of model type
  ├── Parameters: IParameterMap[]
  ├── GetParameterMap(name) → IParameterMap
  └── GetParameters() → GenericParameter[]   ← new array per call

GenericParameter [Serializable]
  ├── Name: string
  ├── Value: int
  ├── SetValue(int) → OnUpdate event
  └── OnUpdate: Action

IParameterMap
  ├── Name: string
  ├── Parents: IParameterLink[]
  ├── Cost: int
  └── CostLogic: ParameterLinkCostLogic

IParameterLink
  ├── Parent: string                         ← parent parameter name
  └── Cost: int

IMappedModel
  ├── OnUpdate: Action
  ├── GetParameters() → string[]
  ├── GetValue(string) → int
  ├── GetParents(string) → IParameterLink[]
  ├── GetParameterAsContainer(string) → GenericParameter
  └── Init(ParametersMap)

IDriverMappedParameters : ISystemDriver
  └── SetIndex(Dictionary<string, ParametersMap>)
```

### Map vs Model

| Entity | Purpose | Stores values |
|--------|---------|---------------|
| `ParametersMap` | Link schema (blueprint) | No |
| `IMappedModel` | Data instance | Yes |

`ParametersMap` is an immutable graph description. `IMappedModel` is mutable state created from a map. This separation allows creating multiple independent instances from one schema.

### ParameterLinkCostLogic

| Value | Description |
|-------|-------------|
| `And` | All parent conditions must be met |
| `Or` | One parent is sufficient |
| `Sum` | Costs are summed |

`Cost` interpretation is defined by the application-level controller: threshold, multiplier, upgrade points, etc.

### GenericParameter — Reactivity

`SetValue(int)` checks for value change. When `Value != value`, updates and fires `OnUpdate` via `ActionExt.Fire()`. Setting the same value is a no-op.

---

## Contract

### Input

- Driver (`IDriverMappedParameters`) receives reference to `_parametersMaps` via `SetIndex()`
- Driver populates the dictionary: key — `FullName` of `IMappedModel` type, value — `ParametersMap`

### Output

- `GetParameters<T>()` — `GenericParameter[]` array from map (new instances per call)
- `GetModel<T>()` — creates `T` instance via `Activator.CreateInstance`, initializes with map
- `InitMap(model)` — initializes an existing `IMappedModel` instance

### API

| Method | Description |
|--------|-------------|
| `ParameterMaps.GetParameters<T>()` | Parameter array by model type |
| `ParameterMaps.GetParameters(Type)` | Parameter array by `Type` |
| `ParameterMaps.GetParameters(string)` | Parameter array by `FullName` |
| `ParameterMaps.GetModel<T>()` | New model instance initialized with map |
| `ParameterMaps.GetModel(Type)` | New model instance by `Type` |
| `ParameterMaps.InitMap(IMappedModel)` | Initialize existing instance |

### Constraints

| Constraint | Reason |
|------------|--------|
| `GenericParameter` values are `int` only | Sufficient for most game mechanics |
| Map GUID = model type `FullName` | Unambiguous type → map binding |
| `GetParameters()` creates a new array | Each call yields independent `GenericParameter` instances |
| `GetModel<T>()` requires parameterless constructor | `Activator.CreateInstance(type)` |
| `null` on errors | Type is not `IMappedModel`, map not found, `FullName == null` |

---

## Usage

### Implementing a data model

```csharp
public class CharacterStats : IMappedModel
{
    public event Action OnUpdate;

    private Dictionary<string, GenericParameter> _parameters = new();
    private ParametersMap _map;

    public void Init(ParametersMap map)
    {
        _map = map;
        _parameters.Clear();
        foreach (var param in map.GetParameters())
        {
            param.OnUpdate += () => OnUpdate?.Invoke();
            _parameters[param.Name] = param;
        }
    }

    public string[] GetParameters() => _parameters.Keys.ToArray();
    public int GetValue(string name) => _parameters.TryGetValue(name, out var p) ? p.Value : 0;
    public GenericParameter GetParameterAsContainer(string name) => _parameters.GetValueOrDefault(name);
    public IParameterLink[] GetParents(string name) => _map?.GetParameterMap(name)?.Parents ?? Array.Empty<IParameterLink>();
}
```

### Retrieving a model at runtime

```csharp
// New instance
var stats = ParameterMaps.GetModel<CharacterStats>() as CharacterStats;

// Initialize existing
var stats = new CharacterStats();
ParameterMaps.InitMap(stats);

// Work with parameters
var strength = stats.GetParameterAsContainer("Strength");
strength.OnUpdate += () => Console.WriteLine($"Strength: {strength.Value}");
strength.SetValue(10);
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Type does not implement `IMappedModel` | `GetParameters` / `GetModel` → `null` |
| Map not found by `FullName` | `GetParameters` → `null`, `GetModel` → `null` |
| `FullName == null` | `GetModel` / `InitMap` → `null` / no-op |
| `SetValue` with same value | Does not fire `OnUpdate` |
| `GetParameters()` on `ParametersMap` | Each call returns a new array with new `GenericParameter` instances |
| Empty `_parametersMaps` dictionary | All queries → `null` |
