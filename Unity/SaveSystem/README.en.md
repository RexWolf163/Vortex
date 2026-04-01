# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`
**Assembly:** `ru.vortex.unity.save`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the save system. Provides a `PlayerPrefs`-based driver with XML serialization and data compression, plus a UI component for progress display.

Capabilities:

- `SaveSystemDriver` — driver: `PlayerPrefs` storage, XML serialization, compression via `Compress`/`Decompress`
- `SavePreset` — XML-serializable wrapper for `SaveFolder[]`
- `UISaveLoadComponent` — MonoBehaviour for save/load progress display
- Save index: GUID list via `PlayerPrefs` (key `SavesData`)
- Metadata (`SaveSummary`) stored separately from data

Out of scope:

- `SaveController`, `ISaveable`, data models — Core
- Data collection/distribution logic — Core
- Encryption (beyond compression) — application level

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.SaveSystem` | `SaveController`, `IDriver`, `SaveData`, `SaveFolder`, `SaveSummary`, `SaveProcessData` |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `StringExtensions.Compress/Decompress` |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` (in `UISaveLoadComponent`) |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]` attribute |
| `Vortex.Unity.UI.UIComponents` | `UIComponent` (in `UISaveLoadComponent`) |

---

## Architecture

```
SaveSystemDriver : Singleton<SaveSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>   ← in-memory index
  ├── _saveDataIndex → reference to SaveController.SaveDataIndex
  │
  ├── Init()
  │    ├── PlayerPrefs.GetString("SavesData") → "guid1;guid2;..."
  │    └── For each GUID → GetSaveSummary() → Saves
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML)
  │    ├── XML → string → Compress(guid) → PlayerPrefs "Save-{guid}"
  │    ├── SaveSummary → XML → PlayerPrefs "SaveSummary-{guid}"
  │    └── Update "SavesData"
  │
  ├── Load(guid)
  │    ├── PlayerPrefs "Save-{guid}" → Decompress(guid) → XML
  │    ├── XML → SavePreset → _saveDataIndex
  │    └── Each SaveFolder → Dictionary<string, string>
  │
  ├── Remove(guid)
  │    ├── Saves.Remove(guid)
  │    ├── PlayerPrefs.DeleteKey "Save-{guid}", "SaveSummary-{guid}"
  │    └── Update "SavesData"
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()

SavePreset [XmlRoot]
  └── Data: List<SaveFolder>                 ← XML-serializable container

UISaveLoadComponent : MonoBehaviour
  ├── title: UIComponent                     ← "Loading" / "Saving"
  ├── progress: UIComponent                  ← formatted progress
  ├── loadingText, savingText: string        ← [LocalizationKey]
  ├── progressTextPattern: string            ← [LocalizationKey], pattern for string.Format
  └── Run() → Coroutine: updates text every frame
```

### PlayerPrefs Storage Format

| Key | Content |
|-----|---------|
| `SavesData` | `"guid1;guid2;guid3"` — all GUIDs joined by `;` |
| `Save-{guid}` | Compressed XML string (`SavePreset`) with compression key = GUID |
| `SaveSummary-{guid}` | XML string (`SaveSummary`) — name and date |

### Compression

Save data is compressed via `string.Compress(guid)` and decompressed via `string.Decompress(guid)`. The GUID serves as the compression key.

### UISaveLoadComponent

Progress display component. On `OnEnable`, starts a Coroutine updating text every frame:
- `title` — "Loading" or "Saving" (based on `SaveController.State`)
- `progress` — formatted string: `string.Format(pattern, globalProgress, globalSize, moduleName, modulePercent)`

---

## Contract

### Input

- Driver registers automatically via `[RuntimeInitializeOnLoadMethod]`
- `SaveController.Save/Load/Remove` delegate to driver

### Output

- Data stored in `PlayerPrefs`
- `GetIndex()` — `Dictionary<string, SaveSummary>` from memory

### Constraints

| Constraint | Reason |
|------------|--------|
| `PlayerPrefs` storage | Size limit depends on platform |
| XML serialization | `SavePreset`, `SaveSummary` — `[XmlRoot]` / `[XmlElement]` |
| Compression uses GUID as key | `Compress`/`Decompress` from `StringExtensions` |
| `Saves` is in-memory Dictionary | Index loaded on `Init()`, updated on `Save`/`Remove` |
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

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| GUID not found in `PlayerPrefs` on `Load` | `LogError`, `_saveDataIndex` remains empty |
| GUID not found on `Remove` | `LogError`, no-op |
| Corrupted XML on deserialization | `SavePreset = null`, `LogError` |
| `PlayerPrefs` overflow | Platform-dependent behavior |
| `UISaveLoadComponent` disabled during process | `OnDisable` → `StopAllCoroutines`, `_process = false` |
| `SaveSummary` GUID not found on `Init` | `LogError`, returns `default(SaveSummary)` |
| `SavesData` empty on `Init` | Empty `Saves`, correct behavior |
| Duplicate GUID on `Save` | `Saves.Add` throws exception (not handled) |
