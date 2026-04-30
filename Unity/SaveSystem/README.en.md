# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem.Drivers.PlayerPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.FileSystemDriver`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`
**Assembly:** `ru.vortex.unity.save`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the save system. Provides two pluggable storage drivers with XML serialization and compression, plus a UI component for progress display. Active driver is selected via `DriverConfig` (codegen-whitelist).

Capabilities:

- `PlayerPrefsDriver/SaveSystemDriver` — `PlayerPrefs`-backed driver
- `FileSystemDriver/FileSystemDriver` — filesystem-backed driver (`FileBus.GetAppPath()/Saves/`)
- `SavePreset` — XML-serializable wrapper for `SaveFolder[]` (shared by both drivers)
- `UISaveLoadComponent` — MonoBehaviour for save/load progress display
- Each driver maintains its own save index and metadata (`SaveSummary`) in its own format

Out of scope:

- `SaveController`, `ISaveable`, data models — Core
- Data collection/distribution logic — Core
- Encryption (beyond compression) — application level

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.SaveSystem` | `SaveController`, `IDriver`, `SaveData`, `SaveFolder`, `SaveSummary`, `SaveProcessData` |
| `Vortex.Core.System` | `Singleton<T>`, `SystemController`, `DriversGenericList.WhiteList` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `StringExtensions.Compress/Decompress`, `IsNullOrWhitespace()` |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` (in `UISaveLoadComponent`) |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]` attribute |
| `Vortex.Unity.UI.UIComponents` | `UIComponent` (in `UISaveLoadComponent`) |
| `Vortex.Unity.FileSystem` | `FileBus.GetAppPath()`, `FileBus.CreateFolders()` (in `FileSystemDriver`) |
| `Vortex.Unity.DriverManagerSystem` | `DriverConfig` asset, `DriversGenericList.cs` codegen |

---

## Active driver selection

Both drivers register automatically via `[RuntimeInitializeOnLoadMethod]`, but `SystemController.SetDriver` validates the candidate against the codegen whitelist `DriversGenericList.WhiteList`, populated from the `DriverConfig` asset. Only the driver explicitly listed in the whitelist for the `SaveController` system is accepted; the others call `Dispose()`.

```
DriverConfig (ScriptableObject in Resources/)
    ↓ codegen
DriversGenericList.cs   (WhiteList: SystemType → DriverType)
    ↓ loaded via reflection on first SetDriver
SystemController.SetDriver(driver) → accepts only whitelisted candidate
```

To switch drivers: open the `DriverConfig` asset, pick the desired `DriverType` for `SaveController`, click "Save Config" — `DriversGenericList.cs` will be regenerated.

---

## Architecture

### Overall structure

```
Vortex/Unity/SaveSystem/
├── Drivers/
│   ├── PlayerPrefsDriver/
│   │   ├── SaveSystemDriver.cs                — partial: IDriver + fields
│   │   ├── SaveSystemDriverExtRun.cs          — [RuntimeInitializeOnLoadMethod]
│   │   └── Editor/SaveSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
│   └── FileSystemDriver/
│       ├── FileSystemDriver.cs                — skeleton, fields
│       ├── FileSystemDriver.Run.cs            — bootstrap, Init
│       ├── FileSystemDriver.Save.cs           — Save + BuildSavePreset
│       ├── FileSystemDriver.Load.cs           — Load, Remove
│       ├── FileSystemDriver.Index.cs          — GetIndex, GetNumberLastSave, ScanIndex
│       ├── FileSystemDriver.Paths.cs          — paths and file names
│       ├── FileSystemDriver.Serialization.cs  — XML serialize/deserialize, Compress
│       └── Editor/FileSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
├── Presets/SavePreset.cs                      — shared XML container
└── View/UISaveLoadComponent.cs                — progress UI
```

### Driver: PlayerPrefs

Stores saves as `PlayerPrefs` keys. Suitable for small saves and platforms with limited file access.

```
SaveSystemDriver : Singleton<SaveSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>     ← in-memory index
  ├── _saveDataIndex → SaveController.SaveDataIndex
  │
  ├── Init()
  │    ├── PlayerPrefs.GetString("SavesData") → "guid1;guid2;..."
  │    └── For each GUID → GetSaveSummary() → Saves
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML) → Compress(guid) → PlayerPrefs "Save-{guid}"
  │    ├── SaveSummary → XML → PlayerPrefs "SaveSummary-{guid}"
  │    └── Update "SavesData", increment "SavesCount"
  │
  ├── Load(guid)
  │    ├── PlayerPrefs "Save-{guid}" → Decompress(guid) → XML → SavePreset
  │    └── SaveFolder → _saveDataIndex
  │
  ├── Remove(guid)
  │    ├── Saves.Remove(guid)
  │    ├── PlayerPrefs.DeleteKey "Save-{guid}", "SaveSummary-{guid}"
  │    └── Update "SavesData"
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()
```

#### PlayerPrefs storage format

| Key | Content |
|-----|---------|
| `SavesData` | `"guid1;guid2;guid3"` — all GUIDs joined by `;` |
| `SavesCount` | `int` — increment counter of the last save |
| `Save-{guid}` | Compressed XML string (`SavePreset`), compression key = GUID |
| `SaveSummary-{guid}` | XML string (`SaveSummary`) — name and date |

### Driver: FileSystem

Stores saves as files on disk. Root path — `FileBus.GetAppPath()/Saves/`. Suitable for large saves and read/write operations without `PlayerPrefs` constraints.

```
FileSystemDriver : Singleton<FileSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>     ← in-memory index
  ├── _saveDataIndex → SaveController.SaveDataIndex
  │
  ├── Init()
  │    └── ScanIndex() → reads all *.summary in Saves/
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML) → Compress(guid) → {guid}.save
  │    ├── SaveSummary → XML → {guid}.summary
  │    └── On new GUID — _increment++ → write to .in file
  │
  ├── Load(guid)
  │    ├── File.ReadAllText({guid}.save) → Decompress(guid) → XML → SavePreset
  │    └── SaveFolder → _saveDataIndex
  │
  ├── Remove(guid)
  │    ├── File.Delete({guid}.save), File.Delete({guid}.summary)
  │    └── Saves.Remove(guid)
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()
```

#### FileSystem storage format

| File | Content |
|------|---------|
| `Saves/{guid}.save` | Compressed XML string (`SavePreset`), compression key = GUID |
| `Saves/{guid}.summary` | XML string (`SaveSummary`) — name and date |
| `Saves/.in` | `int` — increment counter of the last save |

### SavePreset (shared)

```
SavePreset [XmlRoot]
  └── Data: List<SaveFolder>                    ← XML-serializable container
```

Used by both drivers to serialize `SaveFolder[]`.

### UISaveLoadComponent

```
UISaveLoadComponent : MonoBehaviour
  ├── title: UIComponent                        ← "Loading" / "Saving"
  ├── progress: UIComponent                     ← formatted progress
  ├── loadingText, savingText: string           ← [LocalizationKey]
  ├── progressTextPattern: string               ← [LocalizationKey], pattern for string.Format
  └── Run() → Coroutine: updates text every frame
```

---

## Compression

Both drivers compress save body via `string.Compress(guid)` and decompress via `string.Decompress(guid)`. The GUID serves as the compression key. Metadata (`SaveSummary`) and the increment file (`.in`) are **not compressed**.

---

## Contract

### Input

- Drivers register automatically via `[RuntimeInitializeOnLoadMethod]`
- Active driver is selected via `DriverConfig` → `DriversGenericList.WhiteList`
- `SaveController.Save/Load/Remove` delegate to the active driver

### Output

- Data stored according to the active driver's format (PlayerPrefs or files)
- `GetIndex()` — `Dictionary<string, SaveSummary>` from driver memory

### Constraints

| Constraint | Reason |
|------------|--------|
| `PlayerPrefs` storage (PlayerPrefsDriver) | Size limit depends on platform |
| Compression uses GUID as key | `Compress`/`Decompress` from `StringExtensions` |
| Synchronous file operations (FileSystemDriver) | Simplicity; can be made async later for large saves |
| Increment file name — `.in` | Hidden on Unix/Mac, regular on Windows |
| Only one active driver | Enforced by codegen whitelist `DriversGenericList` |
| `UISaveLoadComponent` uses Coroutine | Per-frame update, not UniTask |

---

## Usage

### Progress display

1. Add `UISaveLoadComponent` to a UI element
2. Assign `title` and `progress` (`UIComponent`)
3. Set localization keys: `loadingText`, `savingText`, `progressTextPattern`
4. `progressTextPattern` format: `"{0}/{1} — {2} ({3}%)"` — global progress, module name, module percent

### Working with saves

```csharp
// All saves
var saves = SaveController.GetIndex();

// Save
SaveController.Save("Slot 1");

// Load
SaveController.Load(selectedGuid);

// Remove
SaveController.Remove(selectedGuid);
```

### Switching drivers

1. Open the `DriverConfig` asset in the Inspector (located in `Resources/`).
2. Find the row for the `SaveController` system.
3. Pick `DriverType` — `PlayerPrefsDriver/SaveSystemDriver` or `FileSystemDriver/FileSystemDriver`.
4. Click "Save Config" — `DriversGenericList.cs` will be regenerated.
5. Restart Play or reload the editor domain.

---

## Edge Cases

### Common

| Scenario | Behavior |
|----------|----------|
| No active driver in `DriverConfig` | Whitelist is empty, no driver passes `SetDriver`; `SaveController` ends up with no driver |
| Duplicate GUID on `Save` | PlayerPrefsDriver: `Saves.Add` throws; FileSystemDriver: `Saves[guid] = summary` overwrites, file is rewritten |
| Corrupted XML on deserialization | `SavePreset = null`, `LogError` |
| `UISaveLoadComponent` disabled during process | `OnDisable` → `StopAllCoroutines` |

### PlayerPrefsDriver

| Scenario | Behavior |
|----------|----------|
| GUID not found in `PlayerPrefs` on `Load` | `LogError`, `_saveDataIndex` remains empty |
| GUID not found on `Remove` | `LogError`, no-op |
| `PlayerPrefs` overflow | Platform-dependent behavior |
| Empty `SavesData` on `Init` | Empty `Saves`, correct behavior |

### FileSystemDriver

| Scenario | Behavior |
|----------|----------|
| `Saves/` folder missing on `Save` | Created automatically via `FileBus.CreateFolders` |
| `{guid}.save` missing on `Load` | `LogError`, index unchanged |
| `.in` missing on `GetNumberLastSave` | Created with content `0`, returns `0` |
| Corrupted `{guid}.save` on `Load` | Decompress/XML parser throws, caught with `LogError` |
| `Remove` for non-existent GUID | `LogError`, no-op |
| Disk write error on `Save` | `LogError` via `Debug.LogException`, `Saves` state not updated |
