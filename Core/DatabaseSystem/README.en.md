# DatabaseSystem (Core)

Platform-independent application data bus with GUID-based access.

## Purpose

Centralized storage of **light records** of a common type: GUID indexing, two storage modes (Singleton / MultiInstance), SaveSystem integration, event model, driver interface.

Records are kept **light** — without direct references to heavy assets (see "Extension boundaries") — which lets the whole database stay resident in memory.

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
- Subscribing to `OnInit` after initialization — callback fires immediately (the accessor checks `IsInit` and invokes the delegate at once)
- `GetDataForSave()` returning `null/empty` — record skipped during save
- `Record` is abstract — instances created through the driver

## Extension boundaries

Database is a **bus for shared data**, not an asset store. It has a boundary of applicability: not everything you'd like to "fetch by key" should become a `Record`.

### Rule: light presets only

Database entities are extended with **light presets only**. "Light" means the record does not drag heavy assets (audio clips, textures, prefabs, meshes) along via a **direct serialized reference**. Heavy content is moved into a separate asset and pulled in by linkage (addressable key / path); the `Record` itself holds only the link + light metadata.

The reason is loading. On initialization the driver brings up **all** presets at once (`Resources.LoadAll` / `Addressables` by labels), and direct references to heavy assets cascade them into memory at startup. Light records are cheap to keep resident (a thousand empty SOs ≈ a few–tens of ms); heavy ones are not. So the whole DB can live in memory in full, as long as every record is light and heavy loading is deferred on demand at the asset level (outside Database).

### The "does the record belong on the bus" test

A record is justified in Database if at least one holds:
- **Save** — `GetDataForSave()` returns meaningful state (not `null`). The record takes part in save/load as domain state.
- **MultiInstance** — working copies with mutable state are needed (`GetNewRecord`).
- **Cross-domain** — referenced by GUID from several independent systems or from save data.

If none holds, and the record is essentially an **asset registry** ("a named asset + params") with a single consumer, its home is not Database but the **system's own catalog** (modeled on `EffectsCatalog` in `EffectSpawnSystem`), with linkage through its own key attribute.

`GetDataForSave() => null` is a direct tell: the record does not use the bus's save axis. By itself it is not a verdict (a stateless Singleton config is allowed), but together with "single consumer + it's an asset registry" it means the record's place is a catalog, not the bus.

### Data placement matrix

Two axes: **accessibility** (private / shared) and **weight** (light / heavy).

| | Light | Heavy |
|---|---|---|
| **Shared** | Database (light preset directly) | Database (light record) + heavy asset by linkage, loading/lifetime owned by the consuming view |
| **Private** | The system's own asset-config (not in Database) | Own asset-config + linked heavy asset |

- **Shared-light** → Database. The pure bus case: GUID access, typing, save/MultiInstance.
- **Shared-heavy** → Database holds the light record (identity, params, the link), while the heavy asset is linked and loaded on demand by the consuming system (the view), not by the bus. Database's contract stays synchronous and light; ownership of the asset's lifetime is on the consumer.
- **Private** (regardless of weight) → **not in Database**. A system's local data lives in its own asset-config; heavy content inside it goes by linkage. Putting private data on the shared bus violates "shared → bus, private → inside the component."

> **Note on AudioSystem.** Use `AudioSystem` (sounds/music via `Database`) as the system for **system-wide** sounds — those shared across the whole app (UI clicks, common SFX, menu background music). **Internal** sounds of loadable/unloadable systems (mini-games, individual scenes, cutscenes) are better wired through **linked sound assets** in that system's own config, rather than registered in the shared `Database`. Otherwise a mini-game's local sound sits in the shared bus for the whole session, although it is needed only while the system is loaded. This is a direct application of the "private-heavy → own asset-config + linkage" axis.

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
| Subscribing to `OnInit` after loading | Callback fires immediately |
| `GetDataForSave()` → `null` | Record skipped during save |
| Record in save but not in registry | Ignored during load |
