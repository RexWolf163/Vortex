# DebugSystem (Core)

Global debug mode flag in the settings model.

## Purpose

Partial extension of `SettingsModel` adding the `DebugMode` property. Used by other systems as a global gate for conditional logging.

- Global `DebugMode` — single debug toggle
- Convention: each system adds its own partial with a local flag (`AppStateDebugMode`, `UiDebugMode`, etc.)
- Local flags depend on the global one: `SystemDebugMode => DebugMode && localToggle`

Out of scope: flag value storage and loading (handled by `SettingsSystem`), settings UI, logging implementation.

## Dependencies

- `Vortex.Core.SettingsSystem` — `SettingsModel` (extended class)

## Architecture

```
SettingsModel (partial)
├── DebugSystem/         → DebugMode           (global gate)
├── AppSystem/Debug/     → AppStateDebugMode   (= DebugMode && appStates)
└── UIProviderSystem/    → UiDebugMode         (= DebugMode && uiLogs)
```

### Extension Convention

Each system requiring conditional logging adds a partial `SettingsModel` extension in its `Debug/` folder:

```csharp
// Core: property with private set (populated via SettingsSystem)
public partial class SettingsModel
{
    public bool MySystemDebugMode { get; private set; }
}
```

At the Unity level, a corresponding partial `DebugSettings` adds a toggle and computes the final value:

```csharp
// Unity: toggle + gate
public partial class DebugSettings
{
    [SerializeField] [ToggleButton(isSingleButton: true)] private bool mySystemLogs;
    public bool MySystemDebugMode => DebugMode && mySystemLogs;
}
```

## Contract

### Input
- Value populated via `SettingsSystem` during settings loading

### Output
- `Settings.Data().DebugMode` — `bool`, global flag

### Guarantees
- Property available immediately after `Settings` initialization
- `private set` — modification only through `SettingsSystem`

### Limitations
- `Settings.Data()` may be `null` before initialization — caller must check

## Usage

```csharp
if (Settings.Data().DebugMode)
    Log.Print(LogLevel.Common, "debug info", this);

// Or via a system's local flag:
if (Settings.Data().AppStateDebugMode)
    Log.Print(new LogData(LogLevel.Common, $"AppState: {state}", "App"));
```

## Known Extensions (Core)

| System | Property | File |
|--------|----------|------|
| DebugSystem | `DebugMode` | `Core/DebugSystem/SettingsSystemExt/SettingsModelExtDebug.cs` |
| AppSystem | `AppStateDebugMode` | `Core/AppSystem/Debug/SettingsModelExtDebug.cs` |
| UIProviderSystem | `UiDebugMode` | `Core/UIProviderSystem/Debug/Model/SettingsModelExtDebug.cs` |
