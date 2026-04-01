# SaveSystem (Core)

**Namespace:** `Vortex.Core.SaveSystem.Bus`, `Vortex.Core.SaveSystem.Abstraction`, `Vortex.Core.SaveSystem.Model`
**Assembly:** `ru.vortex.save`
**Platform:** .NET Standard 2.1+

---

## Purpose

Save and load data system. Provides the `SaveController` bus for managing save/load processes, a registry of `ISaveable` modules, and async data collection/distribution. Data is stored as `Dictionary<string, Dictionary<string, string>>` — a hierarchy of module → key → value (strings).

Capabilities:

- `SaveController` — bus: `Save()`, `Load()`, `Remove()`, `GetIndex()`
- `ISaveable` — interface for modules whose data needs saving
- Async data collection on `Save`, async distribution on `Load` (UniTask)
- `SaveProcessData` — two-level progress (global + module)
- Events: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`
- `SaveSummary` — save metadata (name, date, XML-serializable)
- Auto-generation of GUID for new saves

Out of scope:

- Physical storage (PlayerPrefs, files) — Unity layer
- Data compression/encryption — Unity layer (driver)
- UI progress display — Unity layer

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ProcessData` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt.AddNew()` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` on errors |
| UniTask | `UniTask`, `CancellationToken` (in `ISaveable`) |

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
  ├── Save(name, guid?) → async void
  │    ├── State = Saving, OnSaveStart
  │    ├── foreach ISaveable → GetSaveData() → SaveDataIndex
  │    ├── guid ??= Crypto.GetNewGuid()
  │    ├── Driver.Save(name, guid)
  │    └── State = Idle, OnSaveComplete
  │
  ├── Load(guid) → async void
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
  └── GetIndex() → Dictionary<string, SaveSummary>
```

### Data Format

```
SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  └── "ModuleA" → { "key1" → "json1", "key2" → "json2" }
  └── "ModuleB" → { "key1" → "json1" }
```

Each `ISaveable` returns its `GetSaveId()` (module identifier) and `Dictionary<string, string>` (key → JSON string). `SaveController` aggregates all modules into `SaveDataIndex`.

### Save Lifecycle

1. Lock check (`State == Saving` → return)
2. `State = Saving`, `OnSaveStart`
3. `SaveDataIndex.Clear()`
4. For each `ISaveable` — `await GetSaveData(token)` → add to `SaveDataIndex`
5. Generate GUID if not provided
6. `Driver.Save(name, guid)` — physical save
7. `State = Idle`, `OnSaveComplete`

### Load Lifecycle

1. `State = Loading`, `OnLoadStart`
2. `Driver.Load(guid)` — driver populates `SaveDataIndex`
3. For each `ISaveable` — `await OnLoad(token)` (module reads from `SaveController.GetData()`)
4. `State = Idle`, `OnLoadComplete`

### SaveProcessData — Two-Level Progress

| Level | Field | Description |
|-------|-------|-------------|
| Global | `Global.Progress` / `Global.Size` | Current module / total modules |
| Module | `Module.Progress` / `Module.Size` | Progress within current module |

### Data Structures

| Type | Purpose |
|------|---------|
| `SaveData` | struct: `Id`, `Data` — data unit |
| `SaveFolder` | struct: `Id`, `SaveData[]` — module folder |
| `SaveSummary` | struct: `Name`, `Date`, `UnixTimestamp` — save metadata (XML-serializable) |
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
| `SaveController.Save(name, guid?)` | Save (async void) |
| `SaveController.Load(guid)` | Load (async void) |
| `SaveController.Remove(guid)` | Delete save |
| `SaveController.GetData(id)` | Module data by `SaveId` |
| `SaveController.GetIndex()` | All saves |
| `SaveController.Register(ISaveable)` | Register module |
| `SaveController.UnRegister(ISaveable)` | Unregister module |
| `SaveController.GetProcessData()` | Progress data |

### Constraints

| Constraint | Reason |
|------------|--------|
| `Save` locks against re-entry | `State == Saving` → return |
| `Load` has no lock | Repeated calls are not blocked |
| Data is strings only | `Dictionary<string, string>`, JSON serialization is module's responsibility |
| `async void` | `Save`/`Load` are fire-and-forget, exceptions caught internally |
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
| `Save` during saving | Blocked (`State == Saving` → return) |
| `Load` during loading | Not blocked (no lock) |
| `GetData` with non-existent `id` | `Log.Print(Error)`, returns empty `Dictionary` |
| GUID not provided to `Save` | Generated via `Crypto.GetNewGuid()` |
| Exception in `GetSaveData` / `OnLoad` | `Log.Print(Error)`, `State = Idle`, Complete event fires |
| `ISaveable` not registered | Data not collected/distributed |
| `SaveSummary` XML serialization | `Date` as `UnixTimestamp` (long), `DateTime.FromFileTimeUtc` |
