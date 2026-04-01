# SettingsSystem (Core)

**Namespace:** `Vortex.Core.SettingsSystem.Bus`, `Vortex.Core.SettingsSystem.Model`
**Assembly:** `ru.vortex.settings`
**Platform:** .NET Standard 2.1+

---

## Purpose

Project-wide settings access bus. Provides a single read point for `SettingsModel` — an aggregated model assembled from multiple sources via driver. The model is extended through `partial class` — each system adds its own properties.

Capabilities:

- Static settings access: `Settings.Data()`
- `SettingsModel` — extensible partial model
- Driver architecture (`IDriver`) for platform-dependent loading

Out of scope:

- Settings storage (ScriptableObject presets) — Unity layer
- Auto-creation of assets, Editor menus — Unity layer
- UI display of settings

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `SystemModel` |

---

## Architecture

```
Settings : SystemController<Settings, IDriver>
  └── Data() → SettingsModel                 ← Driver.GetData()

IDriver : ISystemDriver
  └── GetData() → SettingsModel

SettingsModel : SystemModel  (partial)
  └── extended by partial classes in other systems
```

### Partial Extension of SettingsModel

`SettingsModel` is an empty partial class in Core. Each system requiring configuration parameters adds its own properties:

| Extension | Properties | System |
|-----------|------------|--------|
| `SettingsModelExtUnity` | `StartScene` | Unity SettingsSystem |
| `SettingsModelExtDebug` | `DebugMode` | Core DebugSystem |
| `SettingsModelExtDatabase` | Database parameters | Unity DatabaseSystem |
| `SettingsModelExtAsyncTweener` | TweenerSystem parameters | Unity TweenerSystem |
| `SettingsModelExtInput` | InputBus parameters | Unity InputBusSystem |
| Others | As needed | Application systems |

Properties are defined as `{ get; private set; }` — populated via `CopyFrom()` (reflection-based copying from `SettingsPreset`).

### Population Mechanism

Driver loads a set of `SettingsPreset` → calls `Model.CopyFrom(preset)` for each → `ObjectExtCopy` copies read-only property values from preset to same-named properties in model via Reflection.

---

## Contract

### Input

- Driver (`IDriver`) connects via `Settings.SetDriver()`
- Driver populates `SettingsModel` from platform-dependent sources

### Output

- `Settings.Data()` — returns `SettingsModel` with aggregated settings
- `null` if driver is not connected

### API

| Method | Description |
|--------|-------------|
| `Settings.Data()` | Aggregated settings model |

### Constraints

| Constraint | Reason |
|------------|--------|
| `Data()` → `null` without driver | `SystemController` returns `null` for `Driver` |
| `SettingsModel` properties are `private set` | Populated only via `CopyFrom` (Reflection) |
| Property name in model = property name in preset | `ObjectExtCopy` matches by name |
| Preset loading order is not guaranteed | On conflict, last loaded wins |

---

## Usage

### Adding settings for a new system

1. Create a partial extension of `SettingsModel`:

```csharp
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
    }
}
```

2. Create a `SettingsPreset` subclass (Unity layer):

```csharp
public class AudioSettings : SettingsPreset
{
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
}
```

3. The asset is auto-created in `Resources/Settings/` on Editor load.

### Reading settings

```csharp
var startScene = Settings.Data().StartScene;
var debugMode = Settings.Data().DebugMode;
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Driver not connected | `Settings.Data()` → `null` |
| `CopyFrom` finds no matching property | Property remains `default` |
| Multiple presets with the same property | Last loaded overwrites the value |
| Property in preset without model counterpart | Ignored |
| `CopyFrom` fails with error | `LogError`, returns `false`, loading aborted |
