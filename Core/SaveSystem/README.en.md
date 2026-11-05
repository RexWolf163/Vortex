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
- Operation queue: `Save`/`Load`/`Remove` run strictly one at a time, with same-type operations on one GUID merged
- `ISaveable` — interface for modules whose data needs saving
- Async data collection on `Save`, async distribution on `Load` (UniTask)
- `SaveProcessData` — two-level progress (global + module)
- Events: `OnSaveRequest`, `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`
- `SaveSummary` — save summary (name, date, app version, optional meta blocks, XML-serializable)
- Summary meta blocks (`SaveController.AddMeta`) — preview, markers and other data from independent controllers, without editing the save name
- `SaveReactor` — an outdated-save correction block: edits the raw string before parsing, within a version window
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

// Save (new GUID). The operation is queued; await — until it completes
var guid = await SaveController.Save("Quick Save");   // null — rejected, cancelled or failed

// Save (overwrite)
await SaveController.Save("Quick Save", existingGuid);

// Load
await SaveController.Load(guid);

// List saves
var saves = SaveController.GetIndex();
foreach (var (guid, summary) in saves)
    Debug.Log($"{summary.Name} — {summary.Date}");

// Remove
SaveController.Remove(guid).Forget();
```

Awaiting an operation is optional: `Forget()` leaves it in the queue, the execution order does not change.

### Summary Metadata

An independent controller subscribes to a save event and writes its own meta block — the save point does not need to know about it:

```csharp
public static class DifficultySaveMeta
{
    public const string MetaKey = "difficulty";

    [RuntimeInitializeOnLoadMethod]
    private static void Init()
    {
        SaveController.OnSaveRequest -= AddMeta;
        SaveController.OnSaveRequest += AddMeta;
    }

    private static void AddMeta() => SaveController.AddMeta(MetaKey, Difficulty.Current.ToString());
}

// Reading — from the summary, without loading the save
var difficulty = summary.GetMeta(DifficultySaveMeta.MetaKey);   // null — no such block
```

A save may run later, after the operations queued before it. Hence there are two windows for writing a meta block:

| Event | When | `AddMeta` writes | For |
|-------|------|------------------|-----|
| `OnSaveRequest` | synchronously inside `Save`, before queuing | into the request metadata — it travels with the operation and reaches the summary at execution | state at request time: preview, mode, save kind |
| `OnSaveStart` | at execution, after `BeginSummary` and the request metadata | straight into the summary of the running cycle | state at write time |

Outside these windows `AddMeta` is useless — the entry is dropped by the next cycle. A repeated key in `OnSaveStart` overwrites the request value. A rejected request does not raise `OnSaveRequest`. Calling `Save` from an `OnSaveRequest` handler is not allowed — the request is rejected with an error in the log. Blocks disabled in settings are discarded by the driver (Unity layer, `SaveSettings`).

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
  ├── Save(name, guid?) → UniTask<string>       ← enqueue
  │    ├── load queued/running → null
  │    ├── OnSaveRequest                  ← request metadata (AddMeta)
  │    └── merge with a pending Save of the same GUID, or a new operation
  ├── Load(guid) → UniTask                      ← enqueue
  ├── Remove(guid) → UniTask                    ← enqueue; cancels pending Saves of the GUID
  │
  ├── Queue (SaveController.Queue.cs): one operation at a time, FIFO
  │    ├── Save:   State = Saving, Driver.BeginSummary(), request metadata, OnSaveStart,
  │    │           foreach ISaveable → GetSaveData() → SaveDataIndex, Driver.Save(name, guid)
  │    ├── Load:   State = Loading, OnLoadStart, Driver.Load(guid), foreach ISaveable → OnLoad()
  │    ├── Remove: Driver.Remove(guid)
  │    └── queue empty → State = Idle; completion event; result to the awaiters
  │
  ├── GetData(id) → Dictionary<string, string>
  ├── GetIndex() → Driver.GetIndex()
  ├── AddMeta(key, metadata) → request metadata (in OnSaveRequest) or Driver.AddMeta
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
  ├── GetNumberLastSave() → int
  ├── BeginSummary()                      ← optional (empty default implementation)
  └── AddMeta(key, metadata)              ← optional (empty default implementation)
```

`BeginSummary` and `AddMeta` extend the contract with default implementations: a driver without metadata support compiles unchanged and writes saves without meta blocks. A supporting driver holds the summary of the current cycle: `BeginSummary` creates a **new** summary with a new list (the copy of the previous one in the save index references the old list — clearing it would wipe that copy), `AddMeta` writes straight into it, `Save` fills in name, date and version and writes it.

### Data Format

```
SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  └── "ModuleA" → { "key1" → "json1", "key2" → "json2" }
  └── "ModuleB" → { "key1" → "json1" }
```

Each `ISaveable` returns its `GetSaveId()` (module identifier) and `Dictionary<string, string>` (key → JSON string). `SaveController` aggregates all modules into `SaveDataIndex`.

### Operation Queue

`Save`, `Load` and `Remove` do not run immediately: they queue an operation and return a task that completes with it. Operations run strictly one at a time, in queuing order; the next one starts after the current one completes. So save and load never overlap, and data collection never runs on top of a load.

Batching — by the last operation on the same GUID (the running operation is merged only where stated):

| Request | What happens |
|---------|--------------|
| `Save(guid)`, the last operation on the GUID is a pending `Save` | Merge: name and metadata from the new request, one result shared by all awaiters |
| `Save()` without a GUID | A new save, never merged |
| `Save` while a load is running or pending | Rejected: `null` and a warning in the log. After the load the state is different — writing it under the requested name is pointless. Saves requested before the load run before it |
| `Load(guid)`, the last operation of **the whole queue** (pending or running) is a `Load` of the same GUID | Merge. Not merged with an earlier load of the same GUID: a load changes the state of the whole game, reordering (A, B, A → B) is not allowed |
| `Remove(guid)` | First, pending `Save`s of this GUID are cancelled from the end of the queue up to the first other operation on it (their result is `null`): writing before removal is pointless, and saves before a load determine what it loads. Then merge with a pending or running `Remove` of the same GUID |

Operations queued from event handlers and from `await` continuations of completed operations are picked up by the same processing loop — the order stays FIFO. Awaiter continuations run synchronously, right after the completion event, before the next operation starts.

An exception in any event handler is logged with the handler name and does not affect other subscribers or the queue.

### Save Lifecycle

1. Request (synchronously inside `Save`): load queued or running → `null`; otherwise `OnSaveRequest` collects the request metadata; the operation is queued or merged
2. Execution: `State = Saving`
3. `Driver.BeginSummary()` — new summary: meta blocks of the previous cycle, failed ones included, are not carried over
4. Request metadata → `Driver.AddMeta`
5. `OnSaveStart` — write-time meta blocks via `AddMeta`
6. `SaveDataIndex.Clear()`
7. For each `ISaveable` — `await GetSaveData(token)` → add to `SaveDataIndex`
8. Generate GUID if not provided
9. `Driver.Save(name, guid)` — physical save together with the cycle's summary
10. Queue empty → `State = Idle`; `OnSaveComplete`; result (GUID, `null` on failure) to the awaiters

### Load Lifecycle

1. Request: merge with the queue tail or enqueue
2. Execution: `State = Loading`, `OnLoadStart`
3. `Driver.Load(guid)` — the driver reads the save body, runs the correction reactors (see below) and populates `SaveDataIndex`
4. For each `ISaveable` — `await OnLoad(token)` (module reads from `SaveController.GetData()`)
5. Queue empty → `State = Idle`; `OnLoadComplete`

### Remove Lifecycle

1. Request: cancel pending `Save`s of the GUID, merge or enqueue
2. Execution: `Driver.Remove(guid)`; `State` does not change
3. Queue empty → `State = Idle`; on success — `OnRemove`

With an empty queue `Remove` runs synchronously on the caller's stack — as before the queue existed.

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
| `SaveSummary` | struct: `Name`, `Date`, `UnixTimestamp`, `Version`, `Meta` — save summary (XML-serializable). `Version` is the app's `Application.version` captured at save time — useful for migrations and for filtering old saves in UI. `Meta` is an optional list of meta blocks (`null` for old saves); read via `GetMeta(key)`, write via `SetMeta(key, value)` (a repeated key overwrites) |
| `SaveMeta` | struct: `Key`, `Value` — a summary meta block; in XML — `<Meta><Entry Key="…">value</Entry></Meta>` |
| `SaveControllerStates` | enum: `Idle`, `Saving`, `Loading` — the kind of the running operation; `Idle` — the queue is empty. `Idle` is not set between queued operations |

---

## Contract

### Input

- `ISaveable` modules register via `Register()`
- `Save(name, guid?)` / `Load(guid)` / `Remove(guid)` queue operations

### Output

- `GetData(id)` — module data after load
- `GetIndex()` — all existing saves (`Dictionary<string, SaveSummary>`)
- Events: `OnSaveRequest`, `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`

### API

| Method | Description |
|--------|-------------|
| `SaveController.Save(name, guid?)` | Queued save, `UniTask<string>`: GUID of the written save; `null` — rejected (a load is queued or running), cancelled by removal of this save, or failed |
| `SaveController.Load(guid)` | Queued load, `UniTask` — completes after the load |
| `SaveController.Remove(guid)` | Queued removal, `UniTask` — completes after the removal |
| `SaveController.GetData(id)` | Module data by `SaveId` |
| `SaveController.GetIndex()` | All saves |
| `SaveController.AddMeta(key, metadata)` | Meta block: in `OnSaveRequest` — into the request metadata, in `OnSaveStart` — into the summary of the running save |
| `SaveController.Register(ISaveable)` | Register module |
| `SaveController.UnRegister(ISaveable)` | Unregister module |
| `SaveController.GetNumberLastSave()` | Last save increment number |
| `SaveController.GetProcessData()` | Progress data |

### Constraints

| Constraint | Reason |
|------------|--------|
| Operations run one at a time | FIFO queue; a repeat is not rejected but waits its turn or is merged |
| `Save` during a load is rejected | The state after the load is different — see [Operation Queue](#operation-queue) |
| Data is strings only | `Dictionary<string, string>`, JSON serialization is module's responsibility |
| `Save`/`Load`/`Remove` return `UniTask` | Caller may `await` or fire-and-forget via `.Forget()`. Exceptions are logged internally |
| New saves (no GUID) are not deduplicated by the queue | Two requests — two saves. Protection against repeated clicks belongs to the save point |
| No operation timeout | A hung `GetSaveData`/`OnLoad` stops the whole queue |
| `CancellationToken` declared but unused | Reserved for future use |

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `Save` during saving | Queued; a pending `Save` of the same GUID — merged |
| `Save` during a load or with a pending load | Rejected: `null`, a warning in the log, `OnSaveRequest` is not raised |
| `Load` during loading | Queued; the same GUID at the queue tail — merged |
| `Load` after a pending `Remove` of the same GUID | Runs after the removal — the driver will not find the save (error in the log), `OnLoadComplete` fires |
| `Remove` with a pending `Save` of the same GUID | The `Save` is cancelled (result `null`), then the removal |
| `Save` from an `OnSaveRequest` handler | Rejected with an error in the log |
| Exception in an event handler | Error in the log with the handler name; other subscribers and the queue continue |
| `GetData` with non-existent `id` | `Log.Print(Error)`, returns empty `Dictionary` |
| GUID not provided to `Save` | Generated via `Crypto.GetNewGuid()` |
| Exception in `GetSaveData` / `OnLoad` | `Log.Print(Error)`, Complete event fires; `Save` returns `null` |
| Write failure swallowed by the driver | `Save` returns the GUID: Core does not learn about the failure — error handling is on the driver side |
| `ISaveable` not registered | Data not collected/distributed |
| `SaveSummary` XML serialization | `Date` as `UnixTimestamp` (long), `DateTime.FromFileTimeUtc` |
| Summary without `<Meta>` (old save, driver without metadata) | `Meta == null`, `GetMeta` returns `null` |
| New summary in an old build | The unknown `<Meta>` element is skipped by the XML serializer |
| `AddMeta` outside the `OnSaveRequest` / `OnSaveStart` windows | Writes into the summary draft, which the next `BeginSummary` replaces — never reaches a save |
| `AddMeta` with a repeated key | The value is overwritten |
| Save failure before `Driver.Save` | The cycle's summary is not written; the next cycle starts with a new one |

---

## Save correction reactors

A mechanism for fixing outdated saves. The data format is known to the module, not to the save system, so Core only provides the seam: the call point, the order and the applicability rule. The actual fixes are written by the data owner.

### How it works

On load the slot driver decompresses the save body and, **before parsing**, passes the string to `SaveReactors.Apply(raw, saveVersion, reactors)`. Reactors run in list order, each returning the string after its own edits; the result goes to the parser.

Editing the raw string is a deliberate choice: after parsing there is nothing left to fix — the build can no longer read a removed type or a renamed field. The save system has no typed stage: `SavePreset` consists of strings, and the modules parse them into objects themselves.

### Applicability

The window is defined by the build version that wrote the save (`SaveSummary.Version`): a reactor applies when the version is not older than `minVersion` and not newer than `maxVersion`. An empty bound means no limit on that side.

Comparison (`SaveVersion.Compare`) is segment-wise by dots, with an arbitrary number of segments. A segment contributes its numeric prefix up to the first non-digit (`260910-rc1` → `260910`); a missing segment counts as zero (`1.0` = `1.0.0`). An empty version is the oldest — that is how saves written before the field existed are read.

### Writing a reactor

```csharp
[Serializable]
public class RenameQuestField : SaveReactor
{
    public override string TransformRaw(string raw) =>
        raw.Replace("<Id>oldName</Id>", "<Id>newName</Id>");
}
```

The reactor is added to the `SaveSettings` list (Unity layer), where its window bounds are set as well. The list is empty by default.

### Guarantees and limits

- The save on disk is **not rewritten**: the correction lives only in the loaded data. The version in the summary changes on the next save.
- The correction runs on every load — the result is not cached.
- A save that falls into no window (including one newer than the build) is read as is.
- A reactor knows nothing about other reactors or about other modules' structure — it locates its own piece in the string itself.
- An exception inside a reactor stops the correction: the block name and stack go to the log, and parsing proceeds with the string produced before the failing block.
- The global storage is not covered: `GlobalContainer` has no version, and the data format there is the module's concern.

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
- **Checking a module** — the `Tools/Vortex/SaveData/Global Index` window (Unity SaveSystem): outside Play Mode it shows the module, its key and problems that make the storage skip it; in Play Mode — current values with live editing.

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
