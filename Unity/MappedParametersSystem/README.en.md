# MappedParametersSystem (Unity)

**Namespace:** `Vortex.Unity.MappedParametersSystem`, `Vortex.Unity.MappedParametersSystem.Base.Preset`, `Vortex.Unity.MappedParametersSystem.Handlers`, `Vortex.Unity.MappedParametersSystem.Attributes`
**Assembly:** `ru.vortex.unity.mappedparameters`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the parametric map system. Provides a ScriptableObject storage for visual parameter graph configuration in Inspector, a Resources-based loading driver, Inspector attributes for parameter and model selection, MonoBehaviour storage, and DOT graph export.

Capabilities:

- `ParametersMapStorage` — ScriptableObject preset for parameter maps
- `MappedParameterPreset` / `MappedParameterLink` — derived parameter and link configuration in Inspector
- `MappedParametersDriver` — loads maps from Resources, registers with `Loader`
- `MappedModelStorage` — abstract MonoBehaviour for holding an `IMappedModel` reference
- `[MappedParameter]` / `[MappedModel]` — Inspector attributes with dropdown selection
- Validation: name uniqueness, parent existence, cycle detection
- Clipboard: JSON export/import of parameter maps
- DOT graph export for Graphviz visualization

Out of scope:

- Data models (`IMappedModel`), bus (`ParameterMaps`) — Core
- Interfaces `IParameterMap`, `IParameterLink`, `ParameterLinkCostLogic` — Core
- Cost interpretation — application level

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.MappedParametersSystem` | `ParameterMaps`, `ParametersMap`, `GenericParameter`, `IMappedModel`, `IParameterMap`, `IParameterLink` |
| `Vortex.Core.System` | `Singleton<T>`, `IDataStorage` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()` for `IProcess` |
| `Vortex.Core.Extensions` | `StringExtensions`, `DictionaryExt` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` for collection display |
| `Vortex.Unity.FileSystem` | `File.CreateFolders()` (Editor) |
| `Vortex.Unity.Extensions` | `OdinDropdownTool` (Editor) |
| Odin Inspector | `[ValueDropdown]`, `[HideReferenceObjectPicker]`, `[InfoBox]`, `OdinAttributeDrawer` |
| UniTask | `UniTask`, `UniTask.Yield()` |

---

## Architecture

```
MappedParametersDriver : Singleton, IDriverMappedParameters, IProcess  (partial)
  ├── Init() → OnInit.Fire()
  ├── SetIndex(Dictionary) → _indexMaps
  ├── RunAsync() → Resources.LoadAll<ParametersMapStorage>
  │    └── GetMap(storage) → ParametersMap
  ├── WaitingFor() → null                    ← no loading dependencies
  ├── [RuntimeInitializeOnLoadMethod] Register()
  └── [InitializeOnLoadMethod] EditorRegister()  ← #if UNITY_EDITOR

ParametersMapStorage : ScriptableObject  (partial)
  ├── guid: string                           ← [ValueDropdown] from IMappedModel types
  ├── baseParams: string[]                   ← base parameters
  ├── mappedParams: MappedParameterPreset[]  ← [SerializeReference] derived
  ├── Editor: Sort(), GetParamsNames(), ReloadMaps()
  ├── ErrorCheck: CheckErrors(), SearchTop() (cycle)
  └── Clipboard: CopyToClipboardAsJson(), LoadFromJson()

MappedParameterPreset : IParameterMap  [Serializable, ClassLabel]
  ├── name: string
  ├── parents: MappedParameterLink[]         ← [SerializeReference]
  ├── costLogic: ParameterLinkCostLogic      ← [HideIf] when ≤1 parent
  ├── Cost: int (property, 0)
  └── Editor: EditorInit(map), Sort(), GetFoldoutName()

MappedParameterLink : IParameterLink  [Serializable]
  ├── parent: string                         ← [ValueDropdown] from map parameters
  ├── cost: int                              ← [Min(1)]
  └── Editor: EditorInit(map, owner), GetParentVariants()

MappedModelStorage : MonoBehaviour, IDataStorage  (abstract)
  ├── _data: IMappedModel
  ├── GetData<T>() → _data as T              ← lazy Init()
  ├── OnUpdateLink: Action (abstract)
  └── Init() (abstract)
```

### Map Loading

`MappedParametersDriver` implements `IProcess` and registers with `Loader`. During `RunAsync`:

1. `Resources.LoadAll<ParametersMapStorage>("")` — loads all presets
2. For each preset, `GetMap()` converts `baseParams[]` + `mappedParams[]` into `IParameterMap[]`
3. Base parameters are wrapped in `MappedParameterPreset(name)` (no parents)
4. Results are placed into `_indexMaps` (dictionary from `ParameterMaps`)

`WaitingFor()` returns `null` — no dependencies on other processes.

### Inspector Integration

- `guid` — `[ValueDropdown]` from all types implementing `IMappedModel` (via Reflection across all assemblies)
- `baseParams` — string array, `[InfoBox]` on empty values
- `mappedParams` — `[SerializeReference]`, each element is `MappedParameterPreset` with `[ClassLabel("$GetFoldoutName")]`
- `parent` in `MappedParameterLink` — `[ValueDropdown]` from all map parameters (excluding owner)
- `costLogic` — displayed only with >1 parent (`[HideIf]`)
- `Sort` button — sorts derived parameters by parent, then by name

### Validation (Editor)

On list changes (`OnListChanged`), `CheckErrors()` executes:

1. Name uniqueness check for base and derived parameters (duplicates renamed: `Name` → `Name_1`)
2. Parent existence check (non-existent parents are cleared)
3. Cycle detection (`SearchTop`) — recursive traversal through parents; cyclic pointers are cleared
4. `Error` flag — empty names or empty parents → red `[InfoBox]`

### Clipboard (Editor)

- `CopyToClipboardAsJson()` — serializes `baseParams` + `mappedParams` to JSON via DTO, copies to clipboard
- `LoadFromJson()` — deserializes from clipboard, restores `MappedParameterPreset[]` + `MappedParameterLink[]`, triggers validation

---

## Contract

### Input

- `ParametersMapStorage` created via `Create > Vortex > Parameters Map`
- `guid` points to `FullName` of `IMappedModel` type
- Base and derived parameters configured in Inspector

### Output

- On loading, driver populates `_indexMaps` in `ParameterMaps`
- Runtime access via `ParameterMaps.GetModel<T>()`, `GetParameters<T>()`, `InitMap()`

### Inspector Attributes

| Attribute | Purpose |
|-----------|---------|
| `[MappedParameter(typeof(T))]` | Dropdown selection of a parameter from type `T` map (Odin drawer) |
| `[MappedModel]` | Dropdown selection of model type from all `ParametersMapStorage` assets (Odin drawer) |

Both drawers support a `Find` button — navigates to the `ParametersMapStorage` asset.

### Constraints

| Constraint | Reason |
|------------|--------|
| Loading from Resources only | `Resources.LoadAll<ParametersMapStorage>` |
| Odin Inspector required | `[ValueDropdown]`, `OdinAttributeDrawer` |
| `WaitingFor() → null` | No dependencies on other processes |
| `Cost` on `MappedParameterPreset` is always 0 | Cost is stored in `MappedParameterLink`, not in the parameter itself |
| Editor validation on every change | Recursive `SearchTop` for cycle detection |

---

## Usage

### Creating a parameter map

1. `Create > Vortex > Parameters Map` — create `ParametersMapStorage`
2. Select model type (`IMappedModel`) in `guid` dropdown
3. Add base parameters (`baseParams`) — string names
4. Add derived parameters (`mappedParams`) — name, parents with costs, `CostLogic` when >1 parent
5. `Sort` button — order by dependencies

### MonoBehaviour storage

```csharp
public class CharacterStatsStorage : MappedModelStorage
{
    public override event Action OnUpdateLink;

    protected override void Init()
    {
        _data = ParameterMaps.GetModel<CharacterStats>();
        _data.OnUpdate += () => OnUpdateLink?.Invoke();
    }
}

// Usage
var storage = GetComponent<CharacterStatsStorage>();
var stats = storage.GetData<CharacterStats>();
```

### Inspector attributes

```csharp
public class SkillButton : MonoBehaviour
{
    [MappedParameter(typeof(CharacterStats))]
    public string targetParameter;

    [MappedModel]
    public string modelType;
}
```

### Clipboard

- Inspector → `To Clipboard` button — JSON to clipboard
- Inspector → `From Clipboard` button — load from clipboard with validation

---

## Editor Tools

### DOT Graph Export

`Menu: Vortex > Debug > Export Mapped Parameters into Graph`

Exports each `ParametersMapStorage` to a `.dot` file (Graphviz). Base parameters are highlighted with `#b3e5fc` color. Links are labeled with cost. Visualization: [Graphviz Online](https://dreampuf.github.io/GraphvizOnline/).

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Duplicate parameter name | Auto-rename `Name` → `Name_1`, error log |
| Parent does not exist in map | Cleared, error log |
| Cyclic dependency | Cyclic pointer cleared, error log |
| Empty clipboard on `LoadFromJson` | Warning, no-op |
| Invalid JSON in clipboard | Error log, state unchanged |
| No `ParametersMapStorage` in Resources | Warning on Editor load, empty `_indexMaps` |
| `MappedParameterPreset` with 0 parents | Allowed — effectively a base parameter in the derived list |
| `[MappedParameter]` with abstract/interface type | `PresetType = null`, red highlight |
| `cost < 1` in `MappedParameterLink` | Clamped by `[Min(1)]` in Inspector |
