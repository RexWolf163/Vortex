# DebugSystem (Unity)

ScriptableObject debug settings asset with toggle buttons for each system.

## Purpose

`DebugSettings` — a `SettingsPreset` (ScriptableObject) storing the global `DebugMode` and local toggles for each system. Partial extensions from other packages add their toggles automatically.

- Global `DebugMode` switch (gate for all local toggles)
- Local toggles: `appStates`, `inputLogs`, `uiLogs`, `asyncTweenerLogs`
- Each local toggle is a partial `DebugSettings` extension from the corresponding system
- Final property: `XxxDebugMode => DebugMode && xxxToggle`
- Menu `Vortex/Configs/Debug Settings` for quick asset access

Out of scope: logging logic, Core `SettingsModel` properties — these belong to Core (Layer 1).

## Dependencies

- `Vortex.Unity.SettingsSystem.Presets` — `SettingsPreset` (base class)
- `Vortex.Unity.EditorTools.Attributes` — `[ToggleButton]`, `[Position]`
- `Sirenix.OdinInspector` (optional) — `[PropertyOrder]`

## Architecture

```
DebugSettings (partial, SettingsPreset)
├── DebugSettings.cs                          — debugMode (global gate)
├── AppSystem/Debug/                          → appStates → AppStateDebugMode
├── InputBusSystem/Debug/Presets/             → inputLogs → InputDebugMode
├── UIProviderSystem/Debug/Presets/           → uiLogs → UiDebugMode
└── UI/TweenerSystem/Debug/Presets/           → asyncTweenerLogs → AsyncTweenerDebugMode

MenuController (Editor)
└── Vortex/Configs/Debug Settings             — asset navigation
```

### Extension Pattern

Each system adds a partial `DebugSettings` with its own toggle:

```csharp
public partial class DebugSettings
{
    [SerializeField] [ToggleButton(isSingleButton: true)] private bool myToggle;
    public bool MyDebugMode => DebugMode && myToggle;
}
```

The `MyDebugMode` property is `true` only when both `DebugMode` AND `myToggle` are enabled.

## Contract

### Input
- `DebugSettings` asset in the project (created as `SettingsPreset`)
- Toggle values — via Inspector

### Output
- `DebugMode` — global bool
- Local properties: `AppStateDebugMode`, `InputDebugMode`, `UiDebugMode`, `AsyncTweenerDebugMode`
- All properties accessible via `Settings.Data()` (copied to `SettingsModel` on load)

### Guarantees
- `[Position(-100)]` — `DebugMode` renders first in Inspector
- All local toggles depend on `DebugMode` — disabling global disables all

### Limitations
- Partial extensions are spread across different packages — full toggle list visible only in the asset Inspector
- Adding a new toggle requires an assembly reference to `ru.vortex.unity.debug`

## Usage

### Configuration

1. Create a `DebugSettings` asset (or use the existing one in `StartSettings`)
2. Enable `DebugMode` (global gate)
3. Enable desired local toggles

### Asset Access

Menu: `Vortex → Configs → Debug Settings`

### Code Check

```csharp
// Via SettingsModel (populated from DebugSettings)
if (Settings.Data().AppStateDebugMode)
    Log.Print(LogLevel.Common, "state changed", "App");
```

### Adding a Toggle for a New System

1. Create `Debug/Presets/DebugSettingsExtMySystem.cs` in the system's folder
2. Add a partial `DebugSettings` with toggle and property
3. In Core — add a partial `SettingsModel` with the corresponding property

## Known Extensions

| System | Toggle | Property | File |
|--------|--------|----------|------|
| DebugSystem | `debugMode` | `DebugMode` | `DebugSystem/DebugSettings.cs` |
| AppSystem | `appStates` | `AppStateDebugMode` | `AppSystem/Debug/DebugSettingsExtApp.cs` |
| InputBusSystem | `inputLogs` | `InputDebugMode` | `InputBusSystem/Debug/Presets/DebugSettingsExtInput.cs` |
| UIProviderSystem | `uiLogs` | `UiDebugMode` | `UIProviderSystem/Debug/Presets/DebugSettingsExtUiProvider.cs` |
| TweenerSystem | `asyncTweenerLogs` | `AsyncTweenerDebugMode` | `UI/TweenerSystem/Debug/Presets/DebugSettingsExtAsyncTweener.cs` |

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `DebugMode = false` | All local `XxxDebugMode` return `false` |
| Asset not created | `Settings.Data()` lacks debug properties — depends on `SettingsSystem` |
| New package without toggle | Debug logs for that package are unmanaged — partial must be added |
