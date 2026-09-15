# SaveSystem (Core)

**Namespace:** `Vortex.Core.SaveSystem`, `Vortex.Core.SaveSystem.Bus`, `Vortex.Core.SaveSystem.Abstraction`, `Vortex.Core.SaveSystem.Model`
**Assembly:** `ru.vortex.save`
**Platform:** .NET Standard 2.1+

---

## Purpose

Save and load data system. Two independent buses:

- `SaveController` — save slots: save/load processes, a registry of `ISaveable` modules, async data collection/distribution. Data is stored as `Dictionary<string, Dictionary<string, string>>` — a hierarchy of module → key → value (strings).
- `GlobalSaveController` — global storage for cross-session data that outlives slots, new games and restarts. Described in [Global storage](#global-storage).

Slot capabilities:

- `SaveController` — bus: `Save()`, `Load()`, `Remove()`, `GetIndex()`
- `ISaveable` — interface for modules whose data needs saving
- Async data collection on `Save`, async distribution on `Load` (UniTask)
- `SaveProcessData` — two-level progress (global + module)
- Events: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`
- `SaveSummary` — save metadata (name, date, app version, XML-serializable)
- Auto-generation of GUID for new saves

Out of scope:

- Physical storage (PlayerPrefs, files) — Unity layer (drivers)
- Slot body compression — Unity layer (slot driver); the global storage container is encoded by the Core controller
- Encryption — not performed; for global storage data it is the module's responsibility
- UI progress display — Unity layer

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ProcessData` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt.AddNew()` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` on errors |
| `Vortex.Core.ComplexModelSystem` | `ComplexModel<T>` — base of `GlobalModel` |
| `Vortex.Core.AppSystem` | `App.OnStateChanged` (immediate write on `Unfocused`/`Stopping`), `App.Exit()` (fail-fast) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — backup count and fail-fast; `SettingsModel` extension via asmref |
| UniTask | `UniTask`, `CancellationToken` (in `ISaveable`, `IProcess`) |

---

## Architecture

```
SaveController : SystemController<SaveController, IDriver>
  ├── SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  │    └── module (SaveId) → { key → value }
  ├── Saveables: HashSet<ISaveable>
  ├── State: SaveControllerStates
  ├── ProcessData: SaveProcessData
  │
  ├── Save(name, guid?) → UniTask<string>
  │    ├── State = Saving, OnSaveStart
  │    ├── foreach ISaveable → GetSaveData() → SaveDataIndex
  │    ├── guid ??= Crypto.GetNewGuid()
  │    ├── Driver.Save(name, guid)
  │    └── State = Idle, OnSaveComplete
  │
  ├── Load(guid) → UniTask
  │    ├── State = Loading, OnLoadStart
  │    ├── Driver.Load(guid) → populates SaveDataIndex
  │    ├── foreach ISaveable → OnLoad()
  │    └── State = Idle, OnLoadComplete
  │
  ├── Remove(guid) → Driver.Remove(guid), OnRemove
  ├── GetData(id) → Dictionary<string, string>
  ├── GetIndex() → Driver.GetIndex()
  ├── Register(ISaveable) / UnRegister(ISaveable)
  └── GetProcessData() → SaveProcessData

ISaveable
  ├── GetSaveId() → string
  ├── GetSaveData(CancellationToken) → UniTask<Dictionary<string, string>>
  ├── GetProcessInfo() → ProcessData
  └── OnLoad(CancellationToken) → UniTask

IDriver : ISystemDriver
  ├── Save(name, guid)
  ├── Load(guid)
  ├── Remove(guid)
  ├── SetIndexLink(Dictionary<string, Dictionary<string, string>>)
  ├── GetIndex() → Dictionary<string, SaveSummary>
  └── GetNumberLastSave() → int
```

### Data Format

```
SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  └── "ModuleA" → { "key1" → "json1", "key2" → "json2" }
  └── "ModuleB" → { "key1" → "json1" }
```

Each `ISaveable` returns its `GetSaveId()` (module identifier) and `Dictionary<string, string>` (key → JSON string). `SaveController` aggregates all modules into `SaveDataIndex`.

### Save Lifecycle

1. Lock check (`State == Saving` → returns `null`)
2. `State = Saving`, `OnSaveStart`
3. `SaveDataIndex.Clear()`
4. For each `ISaveable` — `await GetSaveData(token)` → add to `SaveDataIndex`
5. Generate GUID if not provided
6. `Driver.Save(name, guid)` — physical save
7. `State = Idle`, `OnSaveComplete`

### Load Lifecycle

1. Lock check (`State == Loading` → return)
2. `State = Loading`, `OnLoadStart`
3. `Driver.Load(guid)` — driver populates `SaveDataIndex`
4. For each `ISaveable` — `await OnLoad(token)` (module reads from `SaveController.GetData()`)
5. `State = Idle`, `OnLoadComplete`

### SaveProcessData — Two-Level Progress

| Level | Field | Description |
|-------|-------|-------------|
| Global | `Global.Progress` / `Global.Size` | Current module / total modules |
| Module | `Module.Progress` / `Module.Size` | Progress within current module |

### Data Structures

| Type | Purpose |
|------|---------|
| `SaveData` | struct: `Id`, `Data` — data unit |
| `SaveFolder` | struct: `Id`, `SaveData[] DataSet` — module folder |
| `SaveSummary` | struct: `Name`, `Date`, `UnixTimestamp`, `Version` — save metadata (XML-serializable). `Version` is the app's `Application.version` captured at save time — useful for migrations and for filtering old saves in UI. |
| `SaveControllerStates` | enum: `Idle`, `Saving`, `Loading` |

---

## Contract

### Input

- `ISaveable` modules register via `Register()`
- `Save(name, guid?)` / `Load(guid)` / `Remove(guid)` trigger processes

### Output

- `GetData(id)` — module data after load
- `GetIndex()` — all existing saves (`Dictionary<string, SaveSummary>`)
- Events: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`

### API

| Method | Description |
|--------|-------------|
| `SaveController.Save(name, guid?)` | Save, `UniTask<string>` (returns GUID, or `null` if another Save is in progress) |
| `SaveController.Load(guid)` | Load, `UniTask` |
| `SaveController.Remove(guid)` | Delete save |
| `SaveController.GetData(id)` | Module data by `SaveId` |
| `SaveController.GetIndex()` | All saves |
| `SaveController.Register(ISaveable)` | Register module |
| `SaveController.UnRegister(ISaveable)` | Unregister module |
| `SaveController.GetNumberLastSave()` | Last save increment number |
| `SaveController.GetProcessData()` | Progress data |

### Constraints

| Constraint | Reason |
|------------|--------|
| `Save` locks against re-entry | `State == Saving` → returns `null` |
| `Load` locks against re-entry | `State == Loading` → returns |
| Data is strings only | `Dictionary<string, string>`, JSON serialization is module's responsibility |
| `Save`/`Load` return `UniTask` | Caller may `await` or fire-and-forget via `.Forget()`. Exceptions are logged internally |
| `CancellationToken` declared but unused | Reserved for future use |

---

## Usage

### Implementing ISaveable

```csharp
public class InventoryController : ISaveable
{
    private ProcessData _processData = new("Inventory");

    public string GetSaveId() => "Inventory";

    public async UniTask<Dictionary<string, string>> GetSaveData(CancellationToken ct)
    {
        var data = new Dictionary<string, string>();
        data["items"] = JsonUtility.ToJson(items);
        data["gold"] = gold.ToString();
        return data;
    }

    public ProcessData GetProcessInfo() => _processData;

    public async UniTask OnLoad(CancellationToken ct)
    {
        var data = SaveController.GetData("Inventory");
        if (data.TryGetValue("items", out var json))
            items = JsonUtility.FromJson<ItemList>(json);
        if (data.TryGetValue("gold", out var g))
            gold = int.Parse(g);
    }
}
```

### Save / Load

```csharp
// Register
SaveController.Register(inventoryController);

// Save (new GUID)
SaveController.Save("Quick Save");

// Save (overwrite)
SaveController.Save("Quick Save", existingGuid);

// Load
SaveController.Load(guid);

// List saves
var saves = SaveController.GetIndex();
foreach (var (guid, summary) in saves)
    Debug.Log($"{summary.Name} — {summary.Date}");

// Remove
SaveController.Remove(guid);
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `Save` during saving | Blocked (`State == Saving` → returns `null`) |
| `Load` during loading | Blocked (`State == Loading` → returns without action) |
| `GetData` with non-existent `id` | `Log.Print(Error)`, returns empty `Dictionary` |
| GUID not provided to `Save` | Generated via `Crypto.GetNewGuid()` |
| Exception in `GetSaveData` / `OnLoad` | `Log.Print(Error)`, `State = Idle`, Complete event fires |
| `ISaveable` not registered | Data not collected/distributed |
| `SaveSummary` XML serialization | `Date` as `UnixTimestamp` (long), `DateTime.FromFileTimeUtc` |

---

## Global storage

`GlobalSaveController` stores cross-session data: purchased add-ons, completed quests, overall statistics, time in the application. Data is split into modules (`IGlobalData`); the storage finds them via reflection, the same way `GameModel` finds `IGameData` modules. Slots (`SaveController`) do not touch global data: a new game, loading or deleting a save leaves it intact, and the storage is not listed among saves.

### Architecture

```
GlobalSaveController : SystemController<GlobalSaveController, IGlobalSaveDriver>, IProcess  (partial)
  ├── GlobalModel : ComplexModel<IGlobalData>     ← all modules found via reflection
  ├── module key → module                         ← only modules with a unique non-empty key
  │
  ├── RunAsync()  (Loader, Starting phase)
  │    ├── key index; modules → defaults (in the same instances)
  │    ├── Driver.Read → Decompress → XML → module folders
  │    │    └── unreadable → newest intact copy (when backups > 0)
  │    ├── folders → existing module instances
  │    └── CallOnInit() → IsInit = true, OnInit gate
  │
  ├── Commit<T>()  → write at end of frame (Driver.ScheduleFlush); Unfocused/Stopping — immediately
  ├── Reset<T>() / Reset(Type) / ResetAll()  → defaults in place → immediate write
  ├── OnChanged(IReadOnlyList<Type>)          ← one notification per write
  └── Stopping → write pending changes + backup copy (one per launch)

IGlobalData  [POCO]
  └── GetGlobalKey() → string

IGlobalSaveDriver : ISystemDriver
  ├── Read(out data) → GlobalReadStatus (Ok / NoData / Error)
  ├── Write(data)                                  ← atomic
  ├── WriteCopy(id, data) / GetCopies() / ReadCopy(id, out data) / DeleteCopy(id)
  └── ScheduleFlush(Action)                        ← end of frame, a repeat call replaces the previous one
```

The driver is a "dumb" string store: only the controller knows the container format, intact-copy selection and copy rotation. End of frame is an engine concept, so deferring the write is also the driver's job. Drivers live in the Unity layer (`GlobalFileDriver`, `GlobalPrefsDriver`); the accepted driver puts the controller into the `Loader` queue.

Readiness is announced by the controller when reading completes; the driver does not raise `OnInit`. Hence `IsInit` means "storage has been read", and `WaitingFor(typeof(GlobalSaveController))` in `Loader` waits exactly for the read.

### Format

`GlobalContainer` — XML modelled on `SavePreset`: a list of `SaveData` "module key → Vortex serializer string". The container is compressed with `Compress(…, "global")`, like a slot body. No encryption: the file opens in an archiver, protection against editing is the module's responsibility. The container carries no application version: data format is the module's responsibility.

### Lifecycle

1. **Before loading** `Get<T>()` returns defaults — those set by the module constructor.
2. **Loading (Starting).** Saved data is uploaded into the existing module instances — references obtained earlier stay valid. No container — first launch, defaults. An unreadable container is restored from the newest intact copy (when backups are enabled) and immediately overwritten with it.
3. **Readiness.** `IsInit = true`, `OnInit` subscribers fire. The storage is ready before any slot is loaded.
4. **Commit.** A module changes its data and calls `Commit<T>()`. All commits in a frame — one write and one `OnChanged`. On `Unfocused` and `Stopping` the write and notification happen immediately: there may be no more frames after these transitions, and the order of `Stopping` handlers is undefined.
5. **Shutdown.** Pending changes are written immediately; with backups > 0 one copy per launch is made, the oldest are deleted.

### Settings

| Where | Parameter | Default |
|-------|-----------|---------|
| `DriverConfig`, `GlobalSaveController` row | Driver: file or PlayerPrefs | — (without a driver the storage does not load) |
| `SaveSettings` (Unity) → `SettingsModel.GlobalSaveBackups` | Number of backup copies | 0 — no copies |
| `SaveSettings` (Unity) → `SettingsModel.GlobalSaveFolder` | File folder (file driver) | `Global` |
| `DebugSettings` (Unity) → `SettingsModel.GlobalSaveFailFast` | Fail-fast for read errors, duplicate and empty keys; editor only, independent of `DebugMode` | on |

Fail-fast: loading stops (`App.Exit()`), the gate does not open, writing is blocked — a corrupted file is not overwritten. In builds fail-fast is always off: defaults and a log entry.

### API

| Member | Description |
|--------|-------------|
| `Get<T>()` | Module with current values. Before loading — defaults |
| `HasStoredData<T>()` | The module's data was present in the read container — for migrations |
| `Commit<T>()` / `Commit(Type)` | Commit module changes. Before loading — rejected with an error. The type overload is for tooling |
| `Reset<T>()` / `Reset(Type)` | Module → defaults in the same instance, immediate write, notification |
| `ResetAll()` | Same for all modules |
| `OnInit` / `IsInit` | Readiness gate and flag (storage has been read) |
| `OnChanged` | Changes written: module types. Fires regardless of write success |
| `Modules` | Modules by key — for tooling |

### Data module: guide for authors

```csharp
public class QuestFlagsData : IGlobalData
{
    public List<string> Completed { get; internal set; } = new();

    public string GetGlobalKey() => "MyGame.QuestFlags";
}

// Changes — only by the owning package's controller
var flags = GlobalSaveController.Get<QuestFlagsData>();
if (!flags.Completed.Contains(questId))
{
    flags.Completed.Add(questId);
    GlobalSaveController.Commit<QuestFlagsData>();
}

// Reaction: after loading and on changes
GlobalSaveController.OnInit += RefreshBackground;
GlobalSaveController.OnChanged += types =>
{
    if (types.Contains(typeof(QuestFlagsData)))
        RefreshBackground();
};
```

Rules:

- **A parameterless constructor is required** — it sets the defaults. A module without one is not registered.
- **The key is stable.** Changing it after release loses the module's data. Renaming or moving the module class is safe: on read the type marker is replaced with the current type. Names of nested POCO types are the module's responsibility.
- **Data is properties** with a getter and a setter (serializer rules). Fields are not saved and are not reset.
- **The module decides when to commit:** a quest fact — at completion, a purchase cache — on the platform's response.
- **Format changes** are the module's responsibility: missing properties take defaults, extra ones are ignored; dictionaries are merged with the current ones, lists and arrays are replaced.
- **Migrations** — via `HasStoredData<T>()`. Example: `AppTimeData` in SDK GameCore migrates from `PlayerPrefs`; the old key is deleted once the next launch has read the migrated value.
- **Encryption is the module's job.** `Crypto.SetCryptoPack` runs PBKDF2 on every call (hundreds of milliseconds), so encrypt only when the data changes.
- **Holding a module reference is fine:** loading and reset change values in the same instance.
- **Checking a module** — the `Tools/Vortex/GlobalData/Index` window (Unity SaveSystem): outside Play Mode it shows the module, its key and problems that make the storage skip it; in Play Mode — current values with live editing.

### Limitations

| Limitation | Reason |
|------------|--------|
| No encryption | Data protection is the module's responsibility; the file can be edited by hand |
| With 0 backups a corrupted container is not restored | The developer opted out of the safety net |
| Data of disabled packages is dropped on the next write | Package settings are structural: disabling is considered final |
| A copy is made only on `Stopping` | A process killed without `Stopping` leaves no copy for that launch |
| Cloud sync does not merge data across devices | Outside the package's scope |

### Edge cases

| Situation | Behavior |
|-----------|----------|
| No driver in `DriverConfig` | No loading, `IsInit = false`; `Get` — defaults, commits rejected with an error |
| First launch (no container) | Defaults; the first commit creates the container |
| Main container unreadable, copies exist | Newest intact copy, warning logged, the main container is immediately overwritten with it |
| Unreadable, no intact copies (or backups off) | Error logged, defaults; the first commit overwrites the container. Editor with fail-fast — loading stops |
| Unreadable data of one module | That module keeps defaults, error logged, the rest are read; fail-fast — as above |
| Two modules with the same key | Both keep defaults, neither is read or written; error logged; fail-fast — as above |
| Empty key or exception in `GetGlobalKey` | Module excluded from the index; error logged; fail-fast — as above |
| Container data without a module | Warning; not read, dropped on the next write |
| Commit or reset before loading | Error logged, operation rejected |
| Several commits in one frame | One write, one `OnChanged` |
| Write error | Error logged; data stays in memory, the next commit retries; `OnChanged` still fires |
| Restart without domain reload | Module instances are kept, values are reset to defaults before reading |
