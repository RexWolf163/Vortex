# VideoSystem (Core)

Screen resolution and display mode controller.

## Purpose

Platform-independent bus for managing device video settings: screen resolution and display mode (fullscreen, windowed, etc.).

- Storage of available resolutions and screen modes registries
- Getting and setting current resolution
- Getting and setting screen mode
- Delegating operations to the driver via `IVideoDriver`

Out of scope: rendering, graphics quality, VSync, frame rate, camera settings.

## Dependencies

- `Vortex.Core.System` — `SystemController<T, TD>`, `Singleton<T>`, `ISystemDriver`

---

## Usage

```csharp
// Subscribe to readiness
VideoController.OnInit += () =>
{
    var resolutions = VideoController.GetResolutionsList();
    var modes = VideoController.GetScreenModes();
    var current = VideoController.GetResolution();
};

// Set resolution
VideoController.SetResolution("1920x1080");

// Set screen mode
VideoController.SetScreenMode("FullScreenWindow");
```

---

## VideoController

Static controller. Inherits `SystemController<VideoController, IVideoDriver>`.

### Architecture

```
VideoController (SystemController<VideoController, IVideoDriver>)
├── AvailableResolutions: List<string>     — available resolutions registry
├── AvailableScreenModes: List<string>     — available modes registry
├── GetResolutionsList(): IReadOnlyList    — read resolutions registry
├── GetScreenModes(): IReadOnlyList        — read modes registry
├── GetResolution(): string                — current resolution
├── SetResolution(string)                  — set resolution
├── GetScreenMode(): string                — current screen mode
├── SetScreenMode(string)                  — set screen mode
└── OnDriverConnect()                      — pass registry references to driver
```

### Contract

**Input:**
- Driver registers via `VideoController.SetDriver(driver)`
- Driver populates `AvailableResolutions` and `AvailableScreenModes` registries during `Init()`

**Output:**
- Available resolutions and modes registries via `GetResolutionsList()` / `GetScreenModes()`
- Current values via `GetResolution()` / `GetScreenMode()`
- `VideoController.OnInit` event after driver initialization

**Guarantees:**
- Registries are passed to the driver by reference via `SetLinks()` — the driver populates them directly
- `OnDriverConnect()` is called before `Driver.Init()`

**Limitations:**
- All `Get`/`Set` methods delegate directly to the driver — `NullReferenceException` if no driver is connected
- Registries are empty until `Driver.Init()` completes

---

## IVideoDriver

Driver interface. Inherits `ISystemDriver`.

| Method | Purpose |
|--------|---------|
| `SetLinks(List<string>, List<string>)` | Receive references to controller registries |
| `SetScreenMode(string)` | Set screen mode |
| `GetScreenMode(): string` | Get current screen mode |
| `SetResolution(string)` | Set resolution |
| `GetResolution(): string` | Get current resolution |

---

## ScreenMode

Screen mode enum. Mirrors `UnityEngine.FullScreenMode` without platform dependency.

| Value | Description |
|-------|-------------|
| `ExclusiveFullScreen` | Exclusive fullscreen (Windows) |
| `FullScreenWindow` | Fullscreen window (all platforms) |
| `Windowed` | Windowed mode (desktop) |
| `MaximizedWindow` | Maximized window (Windows, macOS) |

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `GetResolution()` called before `OnInit` | `NullReferenceException` — driver not connected |
| Driver fails whitelist check | `SetDriver` returns `false`, driver not connected |
| Repeated `SetDriver` with same instance | Skipped (`Driver.Equals` check) |
| Repeated `SetDriver` with different instance | Old driver disconnected, new one initialized |
