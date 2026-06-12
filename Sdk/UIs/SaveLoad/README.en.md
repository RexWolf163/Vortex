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

A UI wrapper around `SaveController` (Core SaveSystem): views for the save list and a single slot, a save-completed popup, plus Save/Load/Remove button handlers. In addition — a screenshot capture pipeline with multi-layer compositing of cameras and canvases, and packing the preview into the save name as a base64 blob.

Capabilities:
- Save list (`SaveListView`) on a `Pool` with asynchronous preview restore
- Slot card (`SaveView`) with focus highlighting, `auto_N` / `manual_N` name template, and localized text
- Save/Load/Remove buttons as `MonoBehaviour` handlers on `UIComponent`
- Multi-layer screen capture (`CameraCaptureHandler`) with src_over blending via `Hidden/AlphaBlit`
- Encoding the preview into the save name (`SavePreviewController`, separator `||`, JPEG Medium)
- Save name prefixes: `auto`, `manual` (`SavingSystemConstants`)

Out of scope:
- Storage and serialization of saves themselves (Core `SaveController`)
- The `SaveSummary` structure itself (Core `SaveSystem`)
- The UI-button / pool model proper (`Vortex.Unity.UI.UIComponents`, `PoolSystem`)

## Dependencies

### Core
- `Vortex.Core.SaveSystem.Bus` — `SaveController` (`Save`, `Load`, `Remove`, `GetIndex`, `OnSaveComplete`, `OnLoadComplete`, `OnRemove`, `GetNumberLastSave`)
- `Vortex.Core.SaveSystem.Abstraction` — `SaveSummary`
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

## Architecture

```
SaveLoad/
├── SavePreviewController.cs        ← packs/unpacks preview into save name (base64)
├── SavingSystemConstants.cs        ← AutoName/ManualName prefixes
├── AlphaBlit.shader                ← Hidden/AlphaBlit (src_over)
├── DefineSettings/
│   ├── SdkSettings.SaveLoad.cs     ← partial chunk of SdkSettings (toggle)
│   └── sdk.settings.system.ext.asmref
├── Handlers/
│   ├── CameraCaptureHandler.cs     ← multi-layer screen capture
│   ├── LoadGameHandler.cs          ← load button
│   ├── RemoveSaveGameHandler.cs    ← remove button
│   └── SaveGameHandler.cs          ← save button + preview capture
├── Models/
│   └── SaveSlotData.cs             ← SaveSummary wrapper + lazy Texture2D, IDisposable
├── Views/
│   ├── SaveCompleteView.cs         ← new-save popup
│   ├── SaveListView.cs             ← save list on Pool
│   └── SaveView.cs                 ← single slot card
└── ru.vortex.sdk.game.uis.saveload.asmdef
```

## Components

| Class | Kind | Purpose |
|-------|------|---------|
| `SavePreviewController` | `static class` | `GetPreview(SaveSummary)`, `GetSavePureName(this SaveSummary)`, `GetSaveNameWithPreview(Texture2D, string)`. Separator `||`, JPEG Medium |
| `SavingSystemConstants` | `static class` | Prefix constants: `AutoName = "auto"`, `ManualName = "manual"` |
| `SaveSlotData` | `class : IDisposable` | `SaveSummary` wrapper for `IDataStorage`. `Guid`, `Summary`, lazy `Preview` (Texture2D); `Dispose` destroys the texture |
| `CameraCaptureHandler` | `MonoBehaviour` | A single capture layer (Camera or Canvas). Self-registry `Handlers`, `priority` (0–10), `Render(RT)`, static `Capture()` |
| `SaveGameHandler` | `MonoBehaviour` | Binds a `UIComponent` button to `Save()`. Awaits `WaitForEndOfFrame`, calls `CameraCaptureHandler.Capture()`, builds a `manual_N` name with preview, calls `SaveController.Save(name)` |
| `LoadGameHandler` | `MonoBehaviour` | Reads `SaveSlotData` from `IDataStorage`, calls `SaveController.Load(guid)` |
| `RemoveSaveGameHandler` | `MonoBehaviour` | Subscribes to `IDataStorage.OnUpdateLink`, calls `SaveController.Remove(guid)` |
| `SaveListView` | `MonoBehaviour` | Fills `Pool` with slots ordered by descending `UnixTimestamp`. Holds `StringData _focused` and pushes the current `SaveSlotData` into a `DataStorage`. Re-fills on `OnSaveComplete`/`OnLoadComplete`/`OnRemove`. Disposable via `TimeController.Call` |
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
- Save: `SaveController.Save(name)` where the name contains the base64 preview after `||`
- Load/Remove: `SaveController.Load(guid)` / `SaveController.Remove(guid)`
- Slot preview: `RawImage.texture = SaveSlotData.Preview`

### Guarantees
- `SaveListView` orders the index by descending `UnixTimestamp`, the first item becomes focused
- `SaveSlotData.Preview` is created lazily, `Dispose` destroys the `Texture2D`
- `SaveListView.Dispose` unsubscribes events, cancels the CTS, clears the pool and slots
- `CameraCaptureHandler.Capture()` sorts active handlers by `priority` (lower = bottom), composites via src_over
- `SaveGameHandler` awaits `WaitForEndOfFrame` before capture — the render pipeline is consistent when called from EventSystem
- `RemoveSaveGameHandler` re-initializes on `IDataStorage.OnUpdateLink`

### Limitations
- The `Hidden/AlphaBlit` shader must be in Always Included Shaders (Project Settings → Graphics)
- One `CameraCaptureHandler` carries exactly one entity: either `camera` or `canvas`. If both are null — `LogWarning`
- A `Canvas.ScreenSpaceCamera` without a `worldCamera` — `LogWarning`, the layer is skipped
- A save name must not contain the `||` substring (it is stripped via `Replace`)
- The texture returned by `Capture()` must be destroyed by the caller (`Destroy`)

## Usage

### Save list scene

1. On the UI root place `SaveListView` with references to a `Pool` (slot prefab carrying `SaveView`) and a `DataStorage` for the current focus.
2. On the slot prefab: `SaveView` with `[AutoLink]` to an `IDataStorage` source, references to `slotButton` (`UIComponent`), `slotName`, `timestamp`, `RawImage slotImage`, and the localization keys `autoSavePattern` / `manualSavePattern`.
3. `Load`/`Remove` buttons: `LoadGameHandler` / `RemoveSaveGameHandler` pointing at the same `DataStorage`.

### Save button

```csharp
// Add SaveGameHandler to the GameObject carrying the UIComponent button.
// The save name will look like:  manual_N || <base64 jpeg>
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

### Extracting a preview from an existing save

```csharp
SaveSummary summary = ...;
Texture2D preview = SavePreviewController.GetPreview(summary); // or null
string pureName = summary.GetSavePureName();
```

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Empty save index | `SaveListView` clears the pool, focus is not set |
| `SaveSummary.Name` without `||` | `GetPreview` returns `null`, `GetSavePureName` returns the name as-is |
| Slot name does not match `auto_N` / `manual_N` | The raw `saveName` is shown without localization |
| `IDataStorage` without a `SaveSlotData` | `LoadGameHandler` writes `LogError` and exits; `RemoveSaveGameHandler` unbinds the button |
| `Capture()` with no active handlers | `LogWarning`, returns `null` |
| Screen size changes between frames | `CameraCaptureHandler.Render` recreates the `RenderTexture` |
| `Canvas.ScreenSpaceOverlay` | Captured via a temporary orthographic camera with `cullingMask = 1 << layer`; mode/`worldCamera`/`planeDistance` are restored |
| Disabling `SaveListView` | `Dispose` is scheduled via `TimeController.Call`, unsubscriptions run outside the `OnDisable` frame |
