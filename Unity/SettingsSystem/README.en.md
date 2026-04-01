# SettingsSystem (Unity)

**Namespace:** `Vortex.Unity.SettingsSystem`, `Vortex.Unity.SettingsSystem.Presets`, `Vortex.Unity.SettingsSystem.Editor`
**Assembly:** `ru.vortex.unity.settings`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the settings system. Provides a Resources-based settings loading driver, an abstract ScriptableObject preset for configuration storage, auto-creation of settings assets on Editor load, and a built-in start scene preset.

Capabilities:

- `SettingsDriver` — loads presets from `Resources/Settings/`, populates `SettingsModel` via `CopyFrom`
- `SettingsPreset` — abstract `ScriptableObject` (base class for settings presets)
- `StartSettings` — built-in preset: start scene for Editor
- Auto-creation: on Editor load, creates an asset for every `SettingsPreset` subclass without one
- `StartSceneHandler` — loads start scene on Play Mode in Editor
- Menu `Vortex > Configs > Application Start Config` — navigates to `StartSettings` asset

Out of scope:

- `Settings` bus, `SettingsModel`, `IDriver` interface — Core
- `CopyFrom` logic (Reflection) — `ObjectExtCopy` in Extensions

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.SettingsSystem` | `Settings`, `SettingsModel`, `IDriver` |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.Extensions` | `ObjectExtCopy.CopyFrom()` |
| `Vortex.Unity.FileSystem` | `File.CreateFolders()` |
| `Vortex.Unity.Extensions` | `SoData` (base class), `MenuConfigSearchController` |
| Odin Inspector | `[InfoBox]`, `[ValueDropdown]` (in `StartSettings`) |

---

## Architecture

```
SettingsDriver : Singleton<SettingsDriver>, IDriver  (partial)
  ├── Model → SettingsModel                  ← lazy, created on first access
  ├── Init() → LoadData()
  ├── GetData() → Model
  ├── LoadData()
  │    ├── CheckPath() → create Resources/Settings/
  │    ├── Resources.LoadAll<SettingsPreset>("Settings")
  │    └── foreach preset → Model.CopyFrom(preset)
  ├── [RuntimeInitializeOnLoadMethod] Run()  ← Settings.SetDriver(Instance)
  ├── [InitializeOnLoadMethod] Run()         ← Editor: same
  └── [InitializeOnLoadMethod] EditorRegister()  ← auto-create assets

SettingsPreset : SoData  (abstract ScriptableObject)
  └── Properties { get; } → copied to SettingsModel via CopyFrom

StartSettings : SettingsPreset
  ├── startScene: string                     ← [ValueDropdown] from Build Settings
  └── StartScene → string

StartSceneHandler  (Editor, static)
  └── [RuntimeInitializeOnLoadMethod] Run()
       └── SceneManager.LoadScene(Settings.Data().StartScene)

MenuController  (Editor, static)
  └── [MenuItem("Vortex/Configs/Application Start Config")]
       └── navigate to StartSettings asset
```

### Settings Loading

1. `SettingsDriver.Run()` called via `[RuntimeInitializeOnLoadMethod]` and `[InitializeOnLoadMethod]`
2. `Settings.SetDriver(Instance)` — registers driver in Core bus
3. On first `Model` access — creates `SettingsModel`, calls `LoadData()`
4. `LoadData()` loads all `SettingsPreset` from `Resources/Settings/`
5. For each preset — `Model.CopyFrom(preset)`: Reflection copies read-only properties by name

### Asset Auto-Creation (Editor)

`EditorRegister()` on `[InitializeOnLoadMethod]`:

1. Creates `Resources/Settings/` folder if missing
2. Finds all `SettingsPreset` subclasses via Reflection across all assemblies
3. For each type without an existing asset — `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`
4. Log: `"Create new settings preset {TypeName}"`

### StartSceneHandler (Editor)

On Play Mode in Editor, loads the scene from `Settings.Data().StartScene`. Allows launching the project from any scene without changing Build Settings. Editor-only (`#if UNITY_EDITOR`).

---

## Contract

### Input

- `SettingsPreset` subclasses are created as ScriptableObjects in `Resources/Settings/`
- Assets are auto-created on Editor load
- Values configured in Inspector

### Output

- `Settings.Data()` — `SettingsModel` with aggregated data from all presets
- `SettingsDriver.OnInit` — event after all presets are loaded

### API

| Component | Purpose |
|-----------|---------|
| `SettingsPreset` | Abstract base class for settings presets |
| `StartSettings` | Built-in start scene preset |
| `SettingsDriver.OnInit` | Event on settings load completion |

### Constraints

| Constraint | Reason |
|------------|--------|
| Loading from `Resources/Settings/` | `Resources.LoadAll<SettingsPreset>("Settings")` |
| Auto-creation is Editor-only | `[InitializeOnLoadMethod]` + `AssetDatabase` |
| `StartSceneHandler` is Editor-only | `#if UNITY_EDITOR` |
| One asset per `SettingsPreset` type | `EditorRegister` checks `resources.Contains(type)` |
| Loading order is undefined | `Resources.LoadAll` does not guarantee order |

---

## Usage

### Creating a new settings preset

1. Create a partial extension of `SettingsModel` (Core):

```csharp
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public int MaxPlayers { get; private set; }
    }
}
```

2. Create a `SettingsPreset` subclass (Unity):

```csharp
public class GameplaySettings : SettingsPreset
{
    [SerializeField] private int maxPlayers = 4;
    public int MaxPlayers => maxPlayers;
}
```

3. Reload Editor — `GameplaySettings.asset` is auto-created in `Resources/Settings/`
4. Configure values in Inspector

### Navigating to settings

`Menu: Vortex > Configs > Application Start Config` — opens `StartSettings` asset in Inspector.

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| No asset for a `SettingsPreset` type | Auto-created on Editor load |
| `Resources/Settings/` folder missing | Auto-created via `File.CreateFolders` |
| Driver already set (duplicate `SetDriver`) | Warning log, `Dispose()` of new instance |
| `CopyFrom` finds no matching property | Model property remains `default` |
| `StartScene` is empty | `SceneManager.LoadScene("")` — Unity error |
| Asset manually deleted | Recreated on next Editor load |
| Multiple presets with same property | Last from `LoadAll` overwrites the value |
