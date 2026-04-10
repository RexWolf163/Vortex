# DataOrchestratorSystem

**Namespace:** `Vortex.Unity.UI.Misc.DataOrchestratorSystem`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

Automates distributing a data model across `DataStorage` containers at the GameObject hierarchy level.

Capabilities:
- Generic base class with lifecycle management for binding data to containers
- Orchestrator code generation from a data model (context menu)
- Child GameObject generation with `DataStorage` components (Odin button)
- Automatic `IReactiveData.OnUpdateData` subscription in the base class

Out of scope:
- Data storage and retrieval (`DataStorage`, `IDataStorage`)
- Data mutation logic (game controller or other source)
- Visual presentation (`CounterView`, `SliderView`, etc.)

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData`, `IntData`, `FloatData`, `BoolData`

### Unity
- `Vortex.Unity.UI.Misc` — `DataStorage`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`
- `Sirenix.OdinInspector` — `Button` (Editor hierarchy generation)

## Architecture

```
DataOrchestrator<T> (abstract MonoBehaviour)
├── source: IDataStorage                    ← data model source
├── Data: T                                 ← cached model
├── _storagesIndex: DataStorage[]           ← field cache (reflection in Awake)
├── Map(T) / Unmap()                        ← abstract, bind/unbind data
├── OnDataUpdate()                          ← abstract, update wrappers
├── IReactiveData → OnUpdateData            ← auto-subscription if T implements it
└── [Button] GenerateHierarchy()            ← Editor: create child GOs

OrchestratorScriptGenerator (Editor-only)
└── RMB on .cs → Create → Vortex Templates → DataOrchestrator
    ├── Reflection over public properties of target type
    ├── Generates DataOrchestrator<T> subclass
    └── File created next to the source model
```

## Contract

### Code generation — property mapping rules

| Property type | Handling | Container |
|---------------|----------|-----------|
| `ReactiveValue<T>` subclasses | `SetData` directly | — |
| `IReactiveData` implementations | `SetData` directly | — |
| Reference types (class, interface) | `SetData` directly | — |
| `string` | `SetData` directly | — |
| `int` | Wrapper | `IntData` |
| `float` | Wrapper | `FloatData` |
| `bool` | Wrapper | `BoolData` |
| `enum` | Wrapper with `(int)` cast | `IntData` |
| `Func<>`, `Action<>`, delegates | Skipped | — |
| `struct` (non-primitive, non-enum) | Skipped | — |

### Code generation — exclusions

Properties named `Value`, `State` are skipped. Only properties declared directly on the target type are processed (`DeclaringType == type`); inherited properties are not included.

### Hierarchy generation (Odin button)

- For each `DataStorage` field with an empty reference, a child GameObject is created
- Child GO name: `_{fieldName} [DataStorage]`
- `RectTransform` is removed, only `Transform` remains
- `DataStorage` component is added and linked to the field
- Already linked fields are skipped
- Child GOs are placed at the top of the hierarchy (`SetAsFirstSibling`)

### Base class lifecycle

```
Awake     → cache DataStorage fields via reflection
OnEnable  → subscribe to IDataStorage.OnUpdateLink → Init()
Init      → GetData<T>() → Map(data) → [IReactiveData subscription] → OnDataUpdate()
OnDisable → DeInit() → Unmap() → [IReactiveData unsubscription] → ClearStorages()
```

### Guarantees
- `ClearStorages` nullifies all `DataStorage` containers on `DeInit` — View components receive null
- `IReactiveData` subscription is automatic — if `T` implements the interface, `OnDataUpdate` is called on every update
- `OnDataUpdate` is called immediately after `Map` — initial wrapper population
- Field reflection runs once in `Awake`, then iterates over a cached array

## Usage

### 1. Code generation

1. Select the .cs file with the data model class in Project
2. RMB → `Create → Vortex Templates → DataOrchestrator`
3. `{ClassName}Orchestrator.cs` is created next to the source file
4. Apply manual edits to TODO sections as needed

### 2. Hierarchy generation

1. Add the generated orchestrator to a GameObject
2. Click `Generate Hierarchy` in the Inspector
3. Child GOs with `DataStorage` are created and linked automatically
4. Connect `source` — a component implementing `IDataStorage`

### 3. Manual orchestrator creation

```csharp
public class MyDataOrchestrator : DataOrchestrator<MyGameData>
{
    [SerializeField] private DataStorage score;
    [SerializeField] private DataStorage playerName;

    private IntData _scoreValue = new(0);

    protected override void Map(MyGameData data)
    {
        _scoreValue.Set(data.Score);
        score?.SetData(_scoreValue);
        playerName?.SetData(data.PlayerName);
    }

    protected override void Unmap()
    {
        _scoreValue.Set(0);
    }

    protected override void OnDataUpdate()
    {
        _scoreValue.Set(Data.Score);
    }
}
```

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `source` == null | NRE in `OnEnable` (fail-fast) |
| `GetData<T>()` → null | `Map` is not called, containers remain empty |
| `UpdateLink` (data change) | `DeInit` → `Init` — full reset |
| `T` does not implement `IReactiveData` | `OnDataUpdate` is called only during `Map` (initial population) |
| Repeated `Generate Hierarchy` | Skips already linked fields |
| Inherited model properties | Not included in code generation (only `DeclaringType == type`) |
| Delegates, structs in model | Skipped during code generation |
