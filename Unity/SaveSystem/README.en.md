# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem.Drivers.PlayerPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.FileSystemDriver`, `Vortex.Unity.SaveSystem.Drivers.GlobalPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.GlobalFileDriver`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`, `Vortex.Unity.SaveSystem.Editor`
**Assembly:** `ru.vortex.unity.save`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the save system. Provides pluggable storage drivers for slots (XML serialization and compression) and for the global storage, the shared `SaveSettings` asset, a UI component for progress display and a global storage window. Active drivers are selected via `DriverConfig` (codegen-whitelist) — each bus has its own row.

Capabilities:

- `PlayerPrefsDriver/SaveSystemDriver` — `PlayerPrefs`-backed slot driver
- `FileSystemDriver/FileSystemDriver` — filesystem-backed slot driver (`FileBus.GetAppPath()/{savesFolder}/`, `Saves` by default)
- `GlobalPrefsDriver`, `GlobalFileDriver` — global storage drivers (`GlobalSaveController`)
- `SavePreset` — XML-serializable wrapper for `SaveFolder[]` (shared by the slot drivers)
- `SaveSettings` — shared settings asset: saves folder, backup count, global storage folder and the list of outdated-save correction blocks
- `UISaveLoadComponent` — MonoBehaviour for save/load progress display
- `Tools/Vortex/SaveData/Global Index` window — index of global storage modules; in Play Mode — current values with live editing and reset
- Each slot driver maintains its own save index and metadata (`SaveSummary`) in its own format

Out of scope:

- `SaveController`, `GlobalSaveController`, `ISaveable`, `IGlobalData`, data models — Core
- Data collection/distribution logic, global storage codec and backups — Core
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
| `Vortex.Unity.FileSystem` | `FileBus.GetAppPath()`, `FileBus.CreateFolders()` (in the file drivers) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — folders and backup count from `SaveSettings` |
| `Vortex.Core.LoaderSystem` | `Loader.Register` — the global driver puts the controller into the loading queue |
| `Vortex.Unity.AppSystem` | `TimeController.Call` — end-of-frame global storage write |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — base of `SaveSettings` (asmref into `ru.vortex.unity.settings`) |
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

The global storage has its own row — `GlobalSaveController`: `GlobalFileDriver/GlobalFileDriver` or `GlobalPrefsDriver/GlobalPrefsDriver`. Without it the storage does not load and commits are rejected with an error.

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
│   ├── FileSystemDriver/
│   │   ├── FileSystemDriver.cs                — skeleton, fields
│   │   ├── FileSystemDriver.Run.cs            — bootstrap, Init
│   │   ├── FileSystemDriver.Save.cs           — Save + BuildSavePreset
│   │   ├── FileSystemDriver.Load.cs           — Load, Remove
│   │   ├── FileSystemDriver.Index.cs          — GetIndex, GetNumberLastSave, ScanIndex
│   │   ├── FileSystemDriver.Paths.cs          — paths and file names (folder from SaveSettings)
│   │   ├── FileSystemDriver.Serialization.cs  — XML serialize/deserialize, Compress
│   │   └── Editor/FileSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
│   ├── GlobalPrefsDriver/GlobalPrefsDriver.cs — global storage in PlayerPrefs
│   └── GlobalFileDriver/GlobalFileDriver.cs   — global storage in a file
├── Settings/                                  — asmref → ru.vortex.unity.settings
│   ├── SaveSettings.cs                        — shared settings asset
│   └── SaveSettingsMenu.cs                    — Tools/Vortex/Configs/Save Settings
├── Debug/                                     — asmref → ru.vortex.unity.debug
│   └── DebugSettingsExtGlobalSave.cs          — global storage fail-fast toggle
├── Editor/GlobalDataIndexWindow.cs            — Tools/Vortex/SaveData/Global Index
├── Presets/SavePreset.cs                      — shared slot XML container
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
  │    ├── SaveSummary { Name, Version = Application.version } → XML → PlayerPrefs "SaveSummary-{guid}"
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
| `SaveSummary-{guid}` | XML string (`SaveSummary`) — name, date and `Application.version` captured at save time |

### Driver: FileSystem

Stores saves as files on disk. Root path — `FileBus.GetAppPath()/{savesFolder}/` (`SaveSettings`, `Saves` by default; empty — root). Suitable for large saves and read/write operations without `PlayerPrefs` constraints.

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
  │    ├── SaveSummary { Name, Version = Application.version } → XML → {guid}.summary
  │    └── On new GUID — _increment = GetNumberLastSave() + 1 → write to .in file
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

Paths are given for the default `Saves` folder.

| File | Content |
|------|---------|
| `Saves/{guid}.save` | Compressed XML string (`SavePreset`), compression key = GUID |
| `Saves/{guid}.summary` | XML string (`SaveSummary`) — name, date and `Application.version` captured at save time |
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

### SaveSettings (shared asset)

`SaveSettings` — a `SettingsPreset` in `Resources/Settings`, shared by slots and the global storage; menu `Tools/Vortex/Configs/Save Settings`. The file compiles into the `ru.vortex.unity.settings` assembly (asmref in `Settings/`); values are copied into `SettingsModel`, from where drivers and the Core controller read them.

| Field | `SettingsModel` property | Read by | Default |
|-------|--------------------------|---------|---------|
| `savesFolder` | `SavesFolder` | `FileSystemDriver` | `Saves` |
| `globalSaveBackups` | `GlobalSaveBackups` | `GlobalSaveController` | `0` — no copies |
| `globalSaveFolder` | `GlobalSaveFolder` | `GlobalFileDriver` | `Global` |
| `reactors` | — (read from the preset) | slot drivers | empty |

`reactors` — outdated-save correction blocks (`SaveReactor`, `[SerializeReference]`). The list is not copied into `SettingsModel`: the model extension compiles into the settings assembly, which knows nothing about SaveSystem — a reference would create an assembly cycle. Drivers take the list from the preset itself via `SaveSettings.GetReactors()` (lazy load from `Resources/Settings`; no asset — an empty list). The mechanism is described in the Core SaveSystem README.

Folders are relative to `FileBus.GetAppPath()`; empty — root. The path is joined with `Path.Combine`: an absolute path in the field replaces the root. File drivers read the folder when connected — a change during play applies from the next launch.

### Global storage drivers

Implement `IGlobalSaveDriver` (Core): only string storage and deferring the write to the end of frame — `TimeController.Call(flush, this)`, a repeat call within the frame replaces the previous one. Format, copies and their rotation are handled by `GlobalSaveController`.

Bootstrap: `[RuntimeInitializeOnLoadMethod]` → `GlobalSaveController.SetDriver(Instance)`. The accepted driver registers the controller in `Loader`, a rejected one calls `Dispose()`. The driver does not raise `OnInit`: readiness is announced by the controller after reading.

#### GlobalFileDriver

| File | Content |
|------|---------|
| `{globalSaveFolder}/GlobalSave.dat` | Container (compressed XML) |
| `{globalSaveFolder}/GlobalSave_copy_{id}.dat` | Backup copies; `id` — UTC ticks |
| `*.tmp` | Temporary file of the atomic write |

The write is atomic: a temporary file, then `File.Replace` / `File.Move`. There is no `.summary` extension — the file never shows up in the save list.

#### GlobalPrefsDriver

| Key | Content |
|-----|---------|
| `VortexGlobalSave` | Container |
| `VortexGlobalSave_copy_{id}` | Backup copies |
| `VortexGlobalSave_copies` | Copy list joined by `;` — PlayerPrefs cannot enumerate keys |

Key write atomicity is provided by the platform's PlayerPrefs implementation.

### Global Data window

`Tools/Vortex/SaveData/Global Index` (`Editor/GlobalDataIndexWindow.cs`, editor part of the runtime assembly). The mode depends on Play Mode.

**Outside Play Mode — index of the project's modules.** All `IGlobalData` implementations (`TypeCache`): key, type, assembly, default values (read-only). Problems that make the storage skip a module are flagged:

- no public parameterless constructor — the module will not be found;
- empty key — the module is neither read nor written;
- duplicate key — all modules with that key are neither read nor written (listed).

"Refresh" rebuilds the index; after a recompilation it is rebuilt automatically.

**In Play Mode — storage contents with live editing.** Modules by key (`GlobalSaveController.Modules`):

- a changed value is committed immediately (`GlobalSaveController.Commit(Type)`) — a write and `OnChanged`, just as from module code; subscribers (containers, quest conditions) react as usual;
- "Reset" per module and "Reset all" (with confirmation) — defaults, immediate write;
- while the storage is not loaded or has no driver — a message instead of the list.

"Settings" jumps to `SaveSettings` (in both modes).

**Which properties are shown.** Exactly those the serializer saves: getter and setter, a public getter or `[IsPOCO]`, no `[NotPOCO]`. The property list and value fields come from the shared `EditorTools/DataTools/PocoInspector.cs` — the same one used by the game data window `Tools/Vortex/SaveData/Game Index`.

| Property type | Display |
|---------------|---------|
| `bool`, `int`, `long`, `float`, `double`, `string`, `enum` | Field, editable |
| Collections, nested objects, other types | Serializer string, read-only |

### Fail-fast toggle

`DebugSettings.globalSaveFailFast` (`Debug/`, asmref into `ru.vortex.unity.debug`) → `SettingsModel.GlobalSaveFailFast`. On by default, editor only, not subordinate to `DebugMode`. Behavior is described in the Core SaveSystem README.

---

## Compression and correction

Both drivers compress save body via `string.Compress(guid)` and decompress via `string.Decompress(guid)`. The GUID serves as the compression key. Metadata (`SaveSummary`) and the increment file (`.in`) are **not compressed**.

Load order: read → `Decompress(guid)` → `SaveReactors.Apply(raw, version, SaveSettings.GetReactors())` → XML parsing. The version comes from the summary read at `Init`, so no extra disk access is added; with no summary the save is treated as the oldest. The rule and the reactor format are in the Core SaveSystem README.

---

## Contract

### Input

- Drivers register automatically via `[RuntimeInitializeOnLoadMethod]`
- Active driver is selected via `DriverConfig` → `DriversGenericList.WhiteList`
- `SaveController.Save/Load/Remove` delegate to the active driver
- Saves folder, backup count and global storage folder — `SaveSettings`

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
| Duplicate GUID on `Save` | PlayerPrefsDriver: `Saves.Add` is wrapped in try/catch — the exception is logged (`Debug.LogException`) and not propagated; FileSystemDriver: `Saves[guid] = summary` overwrites, file is rewritten |
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
| `savesFolder` changed | Old saves stay in the previous folder and do not appear in the list: no migration |

### Global storage drivers

| Scenario | Behavior |
|----------|----------|
| `globalSaveFolder` changed | The old file is not read — for the storage it is a first launch (defaults); no migration |
| Folder missing on write | Created via `FileBus.CreateFolders` |
| I/O error on read | `GlobalReadStatus.Error` → the controller's "unreadable" branch |
| Write error | Driver `LogError`, `false` → the controller logs and retries on the next commit |
| `PlayerPrefs` overflow (GlobalPrefsDriver) | Platform-dependent |
