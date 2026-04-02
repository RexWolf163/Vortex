# DatabaseSystem (Unity)

Unity driver implementations for the database, presets, attributes.

## Purpose

Platform adaptation of `Database`: preset loading from Resources / Addressables, ScriptableObject record presets, type-safe record picker attribute for Inspector, Addressable label configuration.

- Two drivers: `ResourcesDriver` and `AddressablesDriver`
- `DatabaseDriverBase` — shared logic (DRY)
- `RecordPreset<T>` — ScriptableObject template for records
- `DbRecordAttribute` — record filtering by type/mode in Inspector
- `DatabaseSettings` — Addressable label configuration
- Partial `Record` extension — adds `Icon` (Sprite)

Out of scope: storage logic, GUID access, saving — these belong to Core (Layer 1).

## Dependencies

- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `IDriver`, `RecordTypes`
- `Vortex.Core.LoaderSystem` — `Loader`, `IProcess`
- `Vortex.Core.SettingsSystem` — `Settings`, `SettingsModel`
- `Vortex.Core.Extensions` — `Crypto.GetNewGuid()`, `AddNew()`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.OdinInspector` — editor attributes
- `UnityEngine.AddressableAssets` (optional, behind `ENABLE_ADDRESSABLES`)

---

## Drivers

### DatabaseDriverBase

Internal static class — shared logic for both drivers.

```
DatabaseDriverBase
├── _recordsLink              — Dictionary<string, Record> (reference to Database registry)
├── _multiInstanceRecordsLink — HashSet<string> (reference to MultiInstance GUIDs)
├── _resourcesIndex           — Dictionary<string, IRecordPreset> (preset cache)
├── SetIndex()                — receive registry references
├── PutData(IRecordPreset)    — add preset to cache + record to registry
├── AddRecord()               — routing: Singleton → _recordsLink, MultiInstance → _multiInstanceRecordsLink
├── GetNewRecord<T>(guid)     — create instance from preset
├── GetNewRecords<T>()        — all MultiInstance copies by type
├── CheckPresetType<T>(guid)  — type compatibility check
└── Clean()                   — clear all caches
```

### ResourcesDriver

Loads from `Resources/Database/`.

```
DatabaseDriver (Singleton<DatabaseDriver>, IDriver, IProcess)
├── DatabaseDriver.cs                    — IDriver API, delegation to DatabaseDriverBase
├── DatabaseDriverExtLoadingSystem.cs    — IProcess: Register, RunAsync
└── DatabaseDriverExtEditor.cs           — IDriverEditor: ReloadDatabase, GetPresetForRecord
```

**Loading:**
1. `[RuntimeInitializeOnLoadMethod]` → `Database.SetDriver(Instance)` + `Loader.Register(Instance)`
2. `RunAsync()` → `Resources.LoadAll("Database")` → filter by `IRecordPreset` → `PutData()` for each
3. `CallOnInit()` → `Database.OnInit`

**WaitingFor:** `Type.EmptyTypes` — no dependencies.

### AddressablesDriver

Loads via Addressables API by labels.

Structure mirrors `ResourcesDriver`. Code behind `#if ENABLE_ADDRESSABLES`.

**Loading:**
1. `[RuntimeInitializeOnLoadMethod]` → `Database.SetDriver(Instance)` + `Loader.Register(Instance)`
2. `RunAsync()` → read labels from `Settings.Data().DatabaseLabels`
3. For each label: `Addressables.LoadAssetsAsync<IRecordPreset>(label, null)`
4. All loaded presets → `PutData()`
5. `CallOnInit()` → `Database.OnInit`
6. `finally` → `Addressables.Release(handle)` for each handle

**Requirements:**
- Package `com.unity.addressables` (define `ENABLE_ADDRESSABLES` set automatically via `DefinitionManager`)
- Labels specified in `DatabaseSettings.databaseLabels`
- Empty labels array — error log, loading skipped

---

## RecordPreset\<T\>

Abstract ScriptableObject — data template for records.

```
RecordPreset<T> (SoData, IRecordPreset) where T : Record, new()
├── type          — RecordTypes (Singleton / MultiInstance)
├── guid          — string (Crypto.GetNewGuid())
├── nameRecord    — string (auto-rename file on change)
├── description   — string
├── icon          — Sprite
├── GetData()     — new T() + CopyFrom(this)
├── CheckRecordType<TU>() / CheckRecordType(Type)
├── ResetGuid()   — [Editor] generate new GUID
└── OnNameChanged() — [Editor] rename asset file
```

### IRecordPreset

```csharp
interface IRecordPreset
{
    RecordTypes RecordType { get; }
    string GuidPreset { get; }
    string Name { get; }
    Record GetData();
    bool CheckRecordType<TU>() where TU : Record;
    bool CheckRecordType(Type type);
}
```

### Record (partial, Unity)

Partial extension of the Core `Record` class, adds:
```csharp
public Sprite Icon { get; protected set; }
```

---

## DbRecordAttribute

Attribute for `string` fields — type-safe record picker in Inspector.

```csharp
[DbRecord]                                              // all records
[DbRecord(typeof(ProductRecord))]                       // by type
[DbRecord(RecordTypes.Singleton)]                       // by mode
[DbRecord(typeof(TemplateRecord), RecordTypes.MultiInstance)] // type + mode
```

Properties:
- `RecordClass` — record type (default: `Record`)
- `RecordType` — nullable `RecordTypes` (default: `null` — all)

---

## DatabaseSettings

`SettingsPreset` for Addressables driver configuration.

- `databaseLabels` — `string[]`, Addressable asset labels
- In Editor: `[ValueDropdown("GetLabels")]` — dropdown from all labels assigned in Addressable Asset Settings
- `SettingsModelExtDatabase` (partial `SettingsModel`) — `DatabaseLabels` property

---

## Usage

### 1. Creating a preset

```csharp
[CreateAssetMenu(menuName = "Database/Product")]
public class ProductPreset : RecordPreset<ProductRecord>
{
    [SerializeField] private float price;
    [SerializeField] private int quantity;

    public float Price => price;
    public int Quantity => quantity;
}
```

For `CopyFrom()` to work correctly, data must be accessible through public getter properties with names matching `Record` properties.

### 2. ResourcesDriver setup

Place presets in `Assets/Resources/Database/`. Assign driver in `DriverConfig`.

### 3. AddressablesDriver setup

1. Label presets in the Addressables window
2. Specify labels in `DatabaseSettings.databaseLabels`
3. Assign driver in `DriverConfig`

### 4. DbRecord attribute

```csharp
[SerializeField, DbRecord(typeof(Sound))]
private string audioSample;
```

### 5. Editor API

```csharp
#if UNITY_EDITOR
var driver = Database.GetDriver() as IDriverEditor;
driver?.ReloadDatabase();
var preset = driver?.GetPresetForRecord(guid);
#endif
```

## Editor Tools

- `RecordPreset.ResetGuid()` — button for new GUID generation
- `RecordPreset.OnNameChanged()` — automatic asset file renaming
- `DbRecordAttribute` — picker with type and mode filtering
- `IDriverEditor.ReloadDatabase()` — cache refresh without restart
- `IDriverEditor.GetPresetForRecord(guid)` — get preset by GUID
- Code generation: `Assets/Create/Vortex Templates/Record`, `Assets/Create/Vortex Templates/Preset for Record`

### Preset for Record — Property Generation

Immutability contract for generated properties:

| Record Property Type | Preset Field | Preset Property |
|---------------------|-------------|-----------------|
| Primitive / immutable | `T field` | `=> field` |
| `List<T>` (T immutable) | `T[] field` | `=> new List<T>(field)` |
| `List<T>` (T reference) | `T[] field` | `=> new List<T>(Array.ConvertAll(field, e => e.DeepCopy()))` |
| Immutable array (`T[]`) | `T[] field` | `=> (T[])field.Clone()` |
| Reference array / other reference | `T field` | `=> field.DeepCopy()` |

Immutability is determined via `ObjectExtDeepClone.IsImmutable` (primitives + platform types from `SimpleTypeMarker`).

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Empty `Resources/Database` folder | Driver initializes without data, `OnInit` fires |
| Empty `databaseLabels` (Addressables) | Error log, loading skipped |
| Duplicate GUID in Singleton | `AddNew` — last one overwrites |
| Duplicate GUID in MultiInstance | Error log, GUID not added again |
| `GetNewRecord` with non-existent GUID | `null` + error log |
| `CheckRecordType` with mismatched type | `false` |
| `SetDriver` returns `false` | Instance destroyed (`Dispose()`) |
| Preset renamed to duplicate | Suffix `(N)`, error log |
| `CancellationToken` during loading | Loading aborted, `OnInit` not called |
