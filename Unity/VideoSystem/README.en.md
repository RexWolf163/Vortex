# VideoSystem (Unity)

Unity driver implementation for the video system.

## Purpose

Platform adaptation of `VideoController`: screen resolution and display mode management via `UnityEngine.Screen`, settings persistence in `PlayerPrefs`, UI components for resolution and mode selection.

- Collecting available device resolutions with deduplication
- Filtering screen modes by platform
- Applying resolution and mode via `Screen.SetResolution`
- Settings save/load through `PlayerPrefs`
- Inspector components: resolution and mode dropdowns

Out of scope: graphics quality, VSync, frame rate, render settings.

## Dependencies

- `Vortex.Core.VideoSystem` — `VideoController` bus, `IVideoDriver`, `ScreenMode`
- `Vortex.Core.System` — `Singleton<T>`
- `Vortex.Core.Extensions` — `ActionExt` (`AddNew`)
- `Vortex.Core.LocalizationSystem` — `StringExt.Translate()` (mode name localization)
- `Vortex.Unity.UI.Misc` — `DropDownComponent`

---

## VideoDriver

`IVideoDriver` implementation. Partial class across two files.

### Architecture

```
VideoDriver (Singleton<VideoDriver>, IVideoDriver)
├── VideoDriver.cs              — Init/Destroy, resolution collection, modes, Set/Get
└── VideoDriverExtLoading.cs    — [RuntimeInitializeOnLoadMethod] auto-registration, Save/Load
```

### Contract

**Input:**
- Automatic registration via `[RuntimeInitializeOnLoadMethod]`
- Controller registry references via `SetLinks()` (called in `OnDriverConnect`)

**Output:**
- Populated `AvailableResolutions` and `AvailableScreenModes` registries in `VideoController`
- `OnInit` event after initialization
- Settings in `PlayerPrefs` (key `VideoSettings`)

**Save format:**

```
{resolution};{screenModeByte}
```

Example: `1920x1080;1`

`resolution` — string in `{width}x{height}` format. `screenModeByte` — numeric `ScreenMode` value.

**Guarantees:**
- Resolutions are deduplicated by `width × height` (refresh rate ignored)
- Modes are filtered by platform via conditional compilation
- Loading with `try/catch` — on corrupt data, settings reset to current screen parameters
- Settings are saved on every resolution or mode change

**Limitations:**
- If `VideoController.SetDriver` returns `false` — instance is destroyed (`Dispose()`)

---

### Platform Mode Filtering

| ScreenMode | Windows | macOS | Linux | Other |
|------------|---------|-------|-------|-------|
| `FullScreenWindow` | + | + | + | + |
| `Windowed` | + | + | + | - |
| `MaximizedWindow` | + | + | - | - |
| `ExclusiveFullScreen` | + | - | - | - |

---

## Handlers

UI components for video settings. Work with `DropDownComponent`.

### ScreenModeHandler

Screen mode dropdown with filtering and localization.

```
ScreenModeHandler (MonoBehaviour)
├── dropDownComponent: DropDownComponent   — dropdown UI
├── localeTagPrefix: string                — localization key prefix
└── whiteList: ScreenMode[]               — allowed modes
```

**Behavior:**
- `Awake` — builds whitelist from `ScreenMode` array
- `OnEnable` — queries available modes from `VideoController`, filters by whitelist, localizes names (`"{prefix}{mode}".Translate()`), passes to `DropDownComponent` with current index
- On selection — calls `VideoController.SetScreenMode()`

### ScreenResolutionHandler

Screen resolution dropdown.

```
ScreenResolutionHandler (MonoBehaviour)
├── dropDownComponent: DropDownComponent   — dropdown UI
└── _list: string[]                        — resolution list copy
```

**Behavior:**
- `OnEnable` — copies resolution list from `VideoController`, passes to `DropDownComponent` with current index
- On selection — calls `VideoController.SetResolution()`

---

## Usage

### Minimal Setup

The driver registers automatically via `[RuntimeInitializeOnLoadMethod]`. No additional actions required.

### Video Settings UI

1. Create a `DropDownComponent` on the scene
2. Add `ScreenResolutionHandler`, assign the `DropDownComponent` reference
3. For modes — add `ScreenModeHandler`, assign `DropDownComponent`, populate `whiteList` with desired modes
4. If needed — set `localeTagPrefix` for mode name localization

### Mode Localization

Localization keys are formed as `{localeTagPrefix}{ScreenMode}`:

```
# Example with localeTagPrefix = "settings.video."
settings.video.FullScreenWindow    → Fullscreen Window
settings.video.Windowed            → Windowed
settings.video.ExclusiveFullScreen → Exclusive Fullscreen
settings.video.MaximizedWindow     → Maximized Window
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| First launch (no save) | Current screen parameters used and saved |
| Saved resolution unavailable | Settings load, but `SetResolution` throws `KeyNotFoundException` on apply |
| Corrupt data in `PlayerPrefs` | `try/catch` → reset to current screen parameters |
| Empty `whiteList` in `ScreenModeHandler` | Empty dropdown list |
| `OnEnable` before `VideoController.OnInit` | Empty registries — dropdown is empty |
