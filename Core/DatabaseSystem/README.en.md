# DatabaseSystem (Core)

Platform-independent application data bus with GUID-based access.

## Purpose

Centralized record storage: GUID indexing, two storage modes (Singleton / MultiInstance), SaveSystem integration, event model, driver interface.

- Indexed record storage (`Dictionary<GUID, Record>`)
- Singleton records: single instance, persistent via `SaveSystem`
- MultiInstance records: fresh copy from preset on each request
- O(1) access by GUID
- Record existence check (`TestRecord`)
- Type filtering (`GetRecords<T>`, `GetMultiInstancePresets<T>`)
- Async save/load via `ISaveable`

Out of scope: preset loading from disk, asset caching, UI for record selection — these are the driver's responsibility (Layer 2).

## Dependencies

- `Vortex.Core.System.Abstractions` — `SystemController`, `Singleton`, `ISystemDriver`, `SystemModel`
- `Vortex.Core.SaveSystem` — `ISaveable`, `SaveController`
- `Vortex.Core.LoaderSystem` — `IProcess`, `ProcessData`
- `Vortex.Core.LoggerSystem` — error logging
- `Cysharp.Threading.Tasks` — `UniTask` (async save operations)

## Architecture

```
Database (partial, SystemController<Database, IDriver>)
├── Database.cs         — registries, access API, OnDriverConnect/Disconnect
├── DatabaseExtSave.cs  — ISaveable: GetSaveData(), OnLoad()
└── DatabaseExtEditor.cs — GetDriver() for editor tools

Record (abstract partial, SystemModel)
├── GuidPreset    — string
├── Name          — string
├── Description   — string
├── GetDataForSave()      — abstract → string
└── LoadFromSaveData()    — abstract ← string

IDriver (ISystemDriver)
├── SetIndex(records, uniqRecords)
├── GetNewRecord<T>(guid)
├── GetNewRecords<T>()
└── CheckPresetType<T>(guid)

IDriverEditor (editor-only)
├── GetPresetForRecord(guid)
└── ReloadDatabase()
```

### Singleton vs MultiInstance

| Type | Storage | Access | Persistence |
|------|---------|--------|-------------|
| `Singleton` | `Dictionary<string, Record>` | `GetRecord<T>(guid)` | Via `SaveSystem` (`ISaveable`) |
| `MultiInstance` | `HashSet<string>` (GUIDs only) | `GetNewRecord<T>(guid)` | None — fresh copy each time |

### IDriver

Platform driver contract:

| Method | Description |
|--------|-------------|
| `SetIndex(records, uniqRecords)` | Receive references to registries for population |
| `GetNewRecord<T>(guid)` | Create new instance from preset |
| `GetNewRecords<T>()` | All new MultiInstance instances by type |
| `CheckPresetType<T>(guid)` | Check preset type compatibility |

### ISaveable (DatabaseExtSave)

- `GetSaveData()` — iterates Singleton records, calls `Record.GetDataForSave()`, skips `null/empty`. Yields every 20 records.
- `OnLoad()` — loads data from `SaveController`, calls `Record.LoadFromSaveData()` for existing records. Records absent from registry are ignored.

### RecordTypes

```csharp
enum RecordTypes { MultiInstance, Singleton }
```

### IRecord

Marker interface (empty).

## Contract

### Input
- Driver registration via `Database.SetDriver(IDriver)`
- Registry population — driver's responsibility

### Output
- `Database.GetRecord<T>(guid)` — Singleton record
- `Database.GetNewRecord<T>(guid)` — new MultiInstance copy
- `Database.GetNewRecords<T>()` — all MultiInstance copies by type
- `Database.GetRecords<T>()` / `GetRecords()` — all Singleton records
- `Database.TestRecord(guid)` — existence check
- `Database.GetMultiInstancePresets<T>()` — GUIDs of all MultiInstance presets by type
- `Database.GetDriver()` — active driver
- Event `Database.OnInit` — after driver data loading

### Guarantees
- `OnDriverConnect` passes registry references and registers with `SaveController`
- `OnDriverDisconnect` unregisters from `SaveController`
- `GetRecord` when requesting MultiInstance as Singleton — `null` + `Error` log
- `GetNewRecord` when requesting Singleton as MultiInstance — `null` + `Error` log
- Non-existent GUID — `null` + `Error` log
- Type mismatch — `null` + `Error` log
- `TestRecord` checks both registries

### Limitations
- Duplicate GUIDs — last one overwrites (driver-dependent)
- Access before initialization — NRE
- Subscribing to `OnInit` after initialization — callback won't fire
- `GetDataForSave()` returning `null/empty` — record skipped during save
- `Record` is abstract — instances created through the driver

## Usage

### Data model creation

```csharp
public class ProductRecord : Record
{
    public float Price { get; set; }
    public int Quantity { get; set; }

    public override string GetDataForSave()
        => this.SerializeProperties();

    public override void LoadFromSaveData(string data)
        => this.CopyFrom(data.DeserializeProperties<ProductRecord>());
}
```

### Data access

```csharp
// Singleton
var product = Database.GetRecord<ProductRecord>("product-guid");
product.Quantity -= 1;

// MultiInstance — fresh copy
var template = Database.GetNewRecord<ProductRecord>("template-guid");

// All records of type
ProductRecord[] all = Database.GetRecords<ProductRecord>();
ProductRecord[] copies = Database.GetNewRecords<ProductRecord>();

// Existence check
bool exists = Database.TestRecord("guid");

// All MultiInstance GUIDs
string[] guids = Database.GetMultiInstancePresets<ProductRecord>();
```

### Initialization subscription

```csharp
Database.OnInit += () =>
{
    var settings = Database.GetRecord<GameSettings>("game-settings");
};
```

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Non-existent GUID | `null` + `Error` log |
| Singleton requested as MultiInstance | `null` + `Error` log |
| MultiInstance requested as Singleton | `null` + `Error` log |
| Type mismatch on `GetRecord<T>` | `null` + `Error` log |
| Driver not assigned | `Instance` not created, all calls — NRE |
| Subscribing to `OnInit` after loading | Callback won't fire |
| `GetDataForSave()` → `null` | Record skipped during save |
| Record in save but not in registry | Ignored during load |
