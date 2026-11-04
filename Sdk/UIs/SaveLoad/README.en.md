# SaveLoad

**Namespace:** `Vortex.Sdk.UIs.SaveLoad`
**Assembly:** `ru.vortex.sdk.game.uis.saveload`

## Activation

The package is enabled through `SdkSettingsSystem`:

- Toggle: `saveLoadWrapperSdk` in the `SdkSettings` asset inspector
- Define symbol: `USING_VORTEX_SAVE_LOAD_WRAPPER`
- Menu: `Tools → Vortex → Configs → SDK Settings`

When the toggle is off, the define is removed from PlayerSettings and the package does not compile (the asmdef declares `defineConstraints: ["USING_VORTEX_SAVE_LOAD_WRAPPER"]`). Activation canon — `Vortex/Sdk/SdkSettingsSystem/README.en.md`.

## Purpose

A UI wrapper around `SaveController` (Core SaveSystem): views for the save list and a single slot, a save-completed popup, plus Save/Load/Remove button handlers. In addition — a screenshot capture pipeline with multi-layer compositing of cameras and canvases, and the preview and save kind as summary meta blocks (`SaveSummary.Meta`).

Capabilities:
- Save list (`SaveListView`) on a `Pool` with asynchronous preview restore
- List filters by summary metadata: meta block key + regular expression for the value; set in the inspector, from code (`SetFilter`/`ClearFilters`/`Refresh`) or by the `SaveListFilterHandler` handler
- Slot card (`SaveView`) with focus highlighting, `auto_N` / `manual_N` name template, and localized text
- Save/Load/Remove buttons as `MonoBehaviour` handlers on `UIComponent`
- Multi-layer screen capture (`CameraCaptureHandler`) with src_over blending via `Hidden/AlphaBlit`
- Save preview — the `preview` meta block (`SavePreviewController`): captured automatically at the start of every save, JPEG Medium in base64. The old form — preview in the name after `||` — is obsolete and read only for old saves
- Save kind — the `kind` meta block (`SaveKindController`): `auto` / `manual`, the save point passes it to `SaveKindController.Save`. For old saves the kind is taken from the name prefix
- Name prefixes and kind values: `auto`, `manual` (`SavingSystemConstants`)

Out of scope:
- Storage and serialization of saves themselves (Core `SaveController`)
- The `SaveSummary` structure itself (Core `SaveSystem`)
- The UI-button / pool model proper (`Vortex.Unity.UI.UIComponents`, `PoolSystem`)

## Dependencies

### Core
- `Vortex.Core.SaveSystem.Bus` — `SaveController` (`Save`, `Load`, `Remove`, `GetIndex`, `AddMeta`, `OnSaveStart`, `OnSaveComplete`, `OnLoadComplete`, `OnRemove`, `GetNumberLastSave`)
- `Vortex.Core.SaveSystem.Abstraction` — `SaveSummary` (`GetMeta`, `SetMeta`)
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.LogicExtensions` — `Base64ToTexture`, `TextureToBase64`, `TextureEncodingRules`
- `Vortex.Core.Extensions.ReactiveValues` — `StringData`
- `Vortex.Core.Extensions.DefaultEnums` — `SwitcherState`
- `Vortex.Core.LocalizationSystem` — `Translate`

### Unity
- `Vortex.Unity.UI.UIComponents` — `UIComponent` (`SetAction`, `SetText`, `SetSwitcher`)
- `Vortex.Unity.UI.PoolSystem` — `Pool`
- `Vortex.Unity.UI.Misc` — `DataStorage`
- `Vortex.Unity.UI.TweenerSystem` — `TweenerHub`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Call`/`RemoveCall`
- `Vortex.Unity.LocalizationSystem` — `[LocalizationKey]`
- `Vortex.Unity.EditorTools.Attributes` — `[ClassFilter]`, `[AutoLink]`

### Sdk
- `Vortex.Sdk.SdkSettingsSystem` — partial `SdkSettings` (activation toggle)

### External
- UniTask — `UniTask`, `UniTask.WaitForEndOfFrame`, `Forget`
- Sirenix Odin Inspector — `[Button]`
- `Hidden/AlphaBlit` shader (Always Included Shaders)

## Usage

### Save list scene

1. On the UI root place `SaveListView` with references to a `Pool` (slot prefab carrying `SaveView`) and a `DataStorage` for the current focus.
2. On the slot prefab: `SaveView` with `[AutoLink]` to an `IDataStorage` source, references to `slotButton` (`UIComponent`), `slotName`, `timestamp`, `RawImage slotImage`, and the localization keys `autoSavePattern` / `manualSavePattern`.
3. `Load`/`Remove` buttons: `LoadGameHandler` / `RemoveSaveGameHandler` pointing at the same `DataStorage`.

### List filters by metadata

A filter is a summary meta block key plus a regular expression for its value. The value is matched against the expression as a whole (`^…$` anchors are added automatically, case-sensitive). The list keeps saves that pass **all** filters; with no filters — all saves.

A missing meta block counts as an empty string — so one expression also covers saves written before metadata existed:

| key | pattern | In the list |
|-----|---------|-------------|
| `gameMode` | `Main\|` | main game and old saves without the marker |
| `gameMode` | `Dlc1` | DLC1 only |
| `gameMode` | `Dlc.*` | any DLC |
| `kind` | `.+` | only saves with a recorded kind |
| `gameMode` | `.*` | everything — the filter cuts nothing |

For `kind`, an empty value on an old save means "kind not recorded", not "manual": `manual|` also lets old autosaves through. An old save's kind is distinguishable only by the name prefix (`SaveKindController.GetKind`), while the filter looks at metadata.

Three ways to set filters:

1. **`SaveListView` inspector** — the `filters` array.
2. **`SaveListFilterHandler`** — a reference to `SaveListView`, a `{key, pattern}` array and the `clearFilters` flag (on by default: first removes the list's filters, including those from its inspector). On enable it sets the filters and rebuilds the list. Enable order relative to the list does not matter: an enabled list is rebuilt, a disabled one builds on enable with the filters already set.
3. **From code:**

```csharp
saveList.ClearFilters();
saveList.SetFilter(GameModeSaveMeta.MetaKey, "Main|");    // previous filters with this key are replaced by one
saveList.SetFilter(SaveKindController.MetaKey, "manual|");
saveList.Refresh();                                        // SetFilter/ClearFilters do not rebuild the list
```

Filters set from code or by the handler change the instance at runtime and do not reach the prefab.

### Save button

```csharp
// Add SaveGameHandler to the GameObject carrying the UIComponent button.
// Save name: manual_N; the summary gets the meta blocks preview (screenshot) and kind = manual.
```

### Custom save point

```csharp
// End of frame is mandatory: the preview is captured at the start of the save (SaveController.OnSaveStart)
await UniTask.WaitForEndOfFrame(this);

// With a kind — the kind meta block is written
var guid = await SaveKindController.Save(SavingSystemConstants.AutoName, $"{SavingSystemConstants.AutoName}_{day}");

// Bypassing SaveKindController — a save without a kind (preview and other meta blocks are written as usual)
var guid2 = await SaveController.Save("custom");
```

### Screen capture

```csharp
// Place a CameraCaptureHandler on each camera / canvas,
// set priority (lower = bottom).
// Add Hidden/AlphaBlit to Always Included Shaders.

await UniTask.WaitForEndOfFrame(this);
var screenshot = CameraCaptureHandler.Capture();
// ... use ...
UnityEngine.Object.Destroy(screenshot);
```

### Preview and kind of an existing save

```csharp
SaveSummary summary = ...;
Texture2D preview = SavePreviewController.GetPreview(summary); // the preview meta block, for old saves — from the name; or null
string kind = SaveKindController.GetKind(summary);             // the kind meta block, for old saves — the name prefix; or null
string pureName = summary.GetSavePureName();                   // strips the preview from old saves
```

The `preview` and `kind` meta blocks can be disabled in `SaveSettings` (Unity SaveSystem, "Save metadata" box). A disabled preview is still captured by `SavePreviewController` — the SDK assembly cannot see the settings, the driver discards the block.

## Architecture

```
SaveLoad/
├── SavePreviewController.cs        ← preview meta block: capture at save start, reading (with fallback from the name)
├── SaveKindController.cs           ← kind meta block: Save(kind, name), GetKind (with fallback from the name)
├── SavingSystemConstants.cs        ← AutoName/ManualName prefixes and kinds
├── AlphaBlit.shader                ← Hidden/AlphaBlit (src_over)
├── DefineSettings/
│   ├── SdkSettings.SaveLoad.cs     ← partial chunk of SdkSettings (toggle)
│   └── sdk.settings.system.ext.asmref
├── Handlers/
│   ├── CameraCaptureHandler.cs     ← multi-layer screen capture
│   ├── LoadGameHandler.cs          ← load button
│   ├── RemoveSaveGameHandler.cs    ← remove button
│   ├── SaveListFilterHandler.cs    ← save list filters on enable
│   └── SaveGameHandler.cs          ← save button (manual)
├── Models/
│   └── SaveSlotData.cs             ← SaveSummary wrapper + lazy Texture2D, IDisposable
├── Views/
│   ├── SaveCompleteView.cs         ← new-save popup
│   ├── SaveListView.cs             ← save list on Pool + metadata filters
│   └── SaveView.cs                 ← single slot card
└── ru.vortex.sdk.game.uis.saveload.asmdef
```

## Components

| Class | Kind | Purpose |
|-------|------|---------|
| `SavePreviewController` | `static class` | Meta block `MetaKey = "preview"`. Subscribed to `SaveController.OnSaveStart`: `CameraCaptureHandler.Capture()` → `EncodePreview` (JPEG Medium, base64) → `SaveController.AddMeta`, the texture is destroyed. `GetPreview(SaveSummary)` — from metadata, otherwise from the name after `\|\|`; `GetSavePureName(this SaveSummary)`. `GetSaveNameWithPreview(Texture2D, string)` — `[Obsolete]`, the old form |
| `SaveKindController` | `static class` | Meta block `MetaKey = "kind"`. `Save(kind, name, guid?)` holds the kind for the cycle and writes it on `OnSaveStart`; `GetKind(SaveSummary)` — from metadata, otherwise from the name prefix (`auto`/`manual`), otherwise `null` |
| `SavingSystemConstants` | `static class` | Name prefix and kind value constants: `AutoName = "auto"`, `ManualName = "manual"` |
| `SaveSlotData` | `class : IDisposable` | `SaveSummary` wrapper for `IDataStorage`. `Guid`, `Summary`, lazy `Preview` (Texture2D); `Dispose` destroys the texture |
| `CameraCaptureHandler` | `MonoBehaviour` | A single capture layer (Camera or Canvas). Self-registry `Handlers`, `priority` (0–10), `Render(RT)`, static `Capture()` |
| `SaveGameHandler` | `MonoBehaviour` | Binds a `UIComponent` button to `Save()`. Awaits `WaitForEndOfFrame` (for the preview capture), calls `SaveKindController.Save(ManualName, "manual_N")` |
| `LoadGameHandler` | `MonoBehaviour` | Reads `SaveSlotData` from `IDataStorage`, calls `SaveController.Load(guid)` |
| `RemoveSaveGameHandler` | `MonoBehaviour` | Subscribes to `IDataStorage.OnUpdateLink`, calls `SaveController.Remove(guid)` |
| `SaveListView` | `MonoBehaviour` | Fills `Pool` with slots ordered by descending `UnixTimestamp`, skipping saves that fail the `filters` (meta block + regular expression, AND). Holds `StringData _focused` and pushes the current `SaveSlotData` into a `DataStorage`. Re-fills on `OnSaveComplete`/`OnLoadComplete`/`OnRemove`. Public `SetFilter(key, pattern)`, `ClearFilters()`, `Refresh()`. Disposable via `TimeController.Call` |
| `SaveListFilterHandler` | `MonoBehaviour` | On `OnEnable` sets the `{key, pattern}` filters on the linked `SaveListView` (after `ClearFilters` if `clearFilters` is checked) and calls `Refresh()` |
| `SaveView` | `MonoBehaviour` | Slot card. Subscribes to `_focused.OnUpdateData`, toggles `SwitcherState.On/Off`. Parses `auto_N` / `manual_N` names and formats via `LocalizationKey` |
| `SaveCompleteView` | `MonoBehaviour` | Popup driven by `SaveController.OnSaveComplete`. Plays a `TweenerHub` array (Back→Forward), shows the last save's name and `Date` |
| `SdkSettings` (partial) | — | Field `saveLoadWrapperSdk` with `[ToggleButton]` and `[DefineSymbol("USING_VORTEX_SAVE_LOAD_WRAPPER")]` |

## Contract

### Input
- Capture layer: `CameraCaptureHandler` with assigned `camera` or `canvas` and `priority`
- Save/Load/Remove UI buttons: `UIComponent` via `SetAction`
- Save list comes from `SaveController.GetIndex()` (`IDictionary<string, SaveSummary>`)
- Selected slot is passed between Views and handlers via `IDataStorage` (`DataStorage` MonoBehaviour) carrying a `SaveSlotData`

### Output
- Save: `SaveKindController.Save(kind, name)` → `SaveController.Save(name)`; the summary gets the `preview` and `kind` meta blocks via `SaveController.AddMeta`
- Load/Remove: `SaveController.Load(guid)` / `SaveController.Remove(guid)`
- Slot preview: `RawImage.texture = SaveSlotData.Preview`

### Guarantees
- `SaveListView` filters the index before sorting and orders it by descending `UnixTimestamp`; the freshest save that passed the filters becomes focused
- `SaveListView.Refresh()` does nothing on a disabled view — the list builds on enable
- A filter expression is compiled once and rebuilt only when its text changes
- `SaveSlotData.Preview` is created lazily, `Dispose` destroys the `Texture2D`
- `SaveListView.Dispose` unsubscribes events, cancels the CTS, clears the pool and slots
- `CameraCaptureHandler.Capture()` sorts active handlers by `priority` (lower = bottom), composites via src_over
- `SaveGameHandler` awaits `WaitForEndOfFrame` before saving — the preview capture on `OnSaveStart` runs in a consistent render pipeline when called from EventSystem
- The save kind lives only within its own cycle: `SaveKindController.Save` resets it on completion, including when the save is rejected by the lock
- Old saves (preview and kind in the name) are read via the `GetPreview` / `GetKind` fallback
- `RemoveSaveGameHandler` re-initializes on `IDataStorage.OnUpdateLink`

### Limitations
- The `Hidden/AlphaBlit` shader must be in Always Included Shaders (Project Settings → Graphics)
- One `CameraCaptureHandler` carries exactly one entity: either `camera` or `canvas`. If both are null — `LogWarning`
- A `Canvas.ScreenSpaceCamera` without a `worldCamera` — `LogWarning`, the layer is skipped
- A save name must not contain the `||` substring: `GetSavePureName` and the `GetPreview` fallback split the name by it
- A save point must wait for the end of the frame before `Save`: the preview is captured synchronously on `OnSaveStart`
- The texture returned by `Capture()` must be destroyed by the caller (`Destroy`)

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Empty save index | `SaveListView` clears the pool, focus is not set |
| No save passes the filters | Same as an empty index |
| Invalid regular expression in a filter | Error logged (key and expression text), the filter passes no saves |
| Several filters with the same key (in the inspector) | All apply (AND); `SetFilter` for that key leaves one |
| `SaveListFilterHandler` without a `SaveListView` reference | Error logged, filters are not set |
| New save (preview in metadata) | `GetPreview` reads the `preview` meta block, the name is clean |
| Old save: `Name` with `||` | `GetPreview` takes the base64 after `||`, `GetSavePureName` strips it |
| Neither a meta block nor `||` in the name | `GetPreview` returns `null`, `GetSavePureName` returns the name as-is |
| Saving with no active `CameraCaptureHandler` | The save is written without the `preview` meta block |
| Saving bypassing `SaveKindController` | No `kind` meta block; `GetKind` tries the name prefix |
| `GetKind` for a save with an arbitrary name and no meta block | `null` |
| Slot name does not match `auto_N` / `manual_N` | The raw `saveName` is shown without localization |
| `IDataStorage` without a `SaveSlotData` | `LoadGameHandler` writes `LogError` and exits; `RemoveSaveGameHandler` unbinds the button |
| `Capture()` with no active handlers | `LogWarning`, returns `null` |
| Screen size changes between frames | `CameraCaptureHandler.Render` recreates the `RenderTexture` |
| `Canvas.ScreenSpaceOverlay` | Captured via a temporary orthographic camera with `cullingMask = 1 << layer`; mode/`worldCamera`/`planeDistance` are restored |
| Disabling `SaveListView` | `Dispose` is scheduled via `TimeController.Call`, unsubscriptions run outside the `OnDisable` frame |
