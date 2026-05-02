# MapLevels

**Namespace:** `Vortex.Sdk.MapLevels`
**Assembly:** `ru.vortex.sdk.maplevels`

## Activation

The package is activated through `SdkSettings` (menu **Vortex → Configs → SDK Settings**), toggle **`mapLevelsSdk`**. The toggle controls the define symbol `USING_VORTEX_MAP_LEVELS`, listed in the asmdef's `defineConstraints` — when disabled, the package is not compiled and its types are unavailable.

Reference for SDK-package activation: `Vortex/Sdk/SdkSettingsSystem/README.en.md`.

Toggle extension file: `DefineSettings/SdkSettings.MapLevels.cs` — partial of `Vortex.Sdk.SdkSettingsSystem.SdkSettings`.

## Purpose

Manages a graph-based level map with lazy prefab instantiation and neighbor streaming. The active level is switched via `Enter(guid)`, direct neighbors (hop=1) are preloaded in the background, distant ones (at distance `>= unloadDistance`) are unloaded.

Capabilities:
- Catalog built from `Database.GetRecords<MapLevelModel>()` on `Init`
- Lazy synchronous instantiation of the active level prefab
- Background neighbor streaming via `UniTask.Yield` (frame-distributed)
- BFS over the `NeighborGuids` graph with retention-depth limit
- Pluggable controller implementation via `MapLevelsSettings.controllerTypeName` (reflection)
- Current level persisted in `MapLevelsGameData` (part of `GameModel` via `IGameData`)
- Scene parent container registration via `MapsView` / `RegisterMapsParent`
- Automatic instance reparenting between scene container and hidden pool (`MapVoid`)

Out of scope:
- Level contents (NPCs, objects, logic) — defined by the prefab
- Spawn points across levels — described by `MapGate`, transition handling is the project's task
- Addressable asset loading — the level prefab is a direct reference in `MapLevelPreset`

## Dependencies

### Core
- `Vortex.Core.AppSystem` — `App`, `AppStates`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `RecordPreset`
- `Vortex.Core.SettingsSystem` — `Settings`, `SettingsModel` (partial extension)
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.Extensions.LogicExtensions` — `IsNullOrWhitespace`, `InitValve`, `[POCO]`, `SerializeProperties`
- `Vortex.Core.Extensions.ReactiveValues` — `EnumData`, `StringData`, `IReactiveData`

### Unity
- `Vortex.Unity.SettingsSystem` — `SettingsPreset`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`, `[DbRecord]`
- `Vortex.Unity.EditorTools` — `[ValueSelector]`
- `Vortex.Unity.UI.StateSwitcher` — `UIStateSwitcher`, `[StateSwitcher]`
- `Vortex.Unity.Extensions.ReactiveValues` — `Vector2Data`

### Sdk
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`
- `Vortex.Sdk.SdkSettingsSystem` — `[DefineSymbol]`

### External
- UniTask — async streaming
- Sirenix Odin Inspector — inspector attributes for `MapGate`

## Architecture

```
ru.vortex.sdk.maplevels
├── Abstractions/
│   └── IMapObject.cs                  ← base map-object contract
├── Bus/
│   └── MapLevelsBus.cs                ← static bus: Controller, Config, MapsParent
├── Config/
│   ├── MapLevelsSettings.cs           ← SettingsPreset (unloadDistance, controllerTypeName)
│   └── SettingsModelExt/
│       ├── MapLevelsConfig.cs         ← runtime config (POCO)
│       └── SettingsModelExtMapLevels.cs ← partial SettingsModel.MapLevels
├── Controllers/
│   ├── MapLevelsController.cs            ← Singleton frame, IsInitialized, Model
│   ├── MapLevelsController.Lifecycle.cs  ← Init / Cleanup, BuildCatalog, GameController binding
│   ├── MapLevelsController.Active.cs     ← Enter, active-level switching
│   └── MapLevelsController.Streaming.cs  ← BFS, EnsureLoaded, Unload, ApplyStreamingPlan
├── DefineSettings/
│   └── SdkSettings.MapLevels.cs       ← partial SdkSettings, mapLevelsSdk toggle
├── Interfaces/
│   ├── IMapLevelsController.cs        ← public controller contract
│   └── IMapLevelView.cs               ← Editor-only MapGate[] provider
├── Model/
│   ├── MapContainer.cs                ← per-level: GameObject Instance + EnumData<State>
│   ├── MapContainerState.cs           ← Empty / Loading / Loaded / Unloading
│   ├── MapGate.cs                     ← struct: id, gatePoint, target (mapID||gateID)
│   ├── MapLevelModel.cs               ← Record: Prefab, NeighborGuids
│   ├── MapLevelViewModel.cs           ← POCO for serializable view state
│   ├── MapLevelsGameData.cs           ← GameModel.IGameData: CurrentLevelGuid
│   └── MapLevelsModel.cs              ← runtime aggregate: Catalog, Containers, ActiveLevelGuid
├── Presets/
│   └── MapLevelPreset.cs              ← RecordPreset<MapLevelModel>: Prefab, NeighborGuids (Singleton)
└── View/
    ├── MapLevelView.cs                ← MonoBehaviour on level prefab, drives UIStateSwitcher
    └── MapsView.cs                    ← scene parent-container registrar
```

### Components

| Class | Type | Purpose |
|-------|-----|-----------|
| `MapLevelsBus` | static | Bus: `Controller`, `Config`, `MapsParent`, `OnReady`, `OnRelease` |
| `MapLevelsController` | `Singleton<T>`, `IMapLevelsController`, partial | Default impl: catalog, active level, streaming |
| `IMapLevelsController` | interface | Controller contract: `Init`, `Enter`, `Cleanup` + events |
| `MapLevelsModel` | `IReactiveData` | Runtime: `Catalog`, `Containers`, `ActiveLevelGuid` |
| `MapLevelsGameData` | `GameModel.IGameData`, `[POCO]` | Saved state: `CurrentLevelGuid` |
| `MapLevelModel` | `Record` | Working level copy: `Prefab`, `NeighborGuids` |
| `MapLevelPreset` | `RecordPreset<MapLevelModel>` | Level preset (forced `Singleton`) |
| `MapContainer` | sealed class | Per-level: `Instance`, `EnumData<MapContainerState>` |
| `MapContainerState` | enum | `Empty`, `Loading`, `Loaded`, `Unloading` |
| `MapGate` | struct | Spawn point: `id`, `gatePoint`, `target` (`mapID‖gateID`) |
| `MapLevelView` | `MonoBehaviour`, `IMapLevelView` | On level prefab — drives `UIStateSwitcher` (`On`/`Off`) |
| `MapsView` | `MonoBehaviour` | Registers/unregisters scene `MapsParent` in the bus |
| `MapLevelsSettings` | `SettingsPreset` | `unloadDistance` (1–16), `controllerTypeName` |
| `MapLevelsConfig` | POCO | Runtime config (built from `MapLevelsSettings`) |
| `IMapObject` | `[POCO]` interface | Base map-object contract (`Vector2Data Position`) |

## Contract

### Input
- `MapLevelsBus.Controller.Enter(guid, gateId = null)` — switch active level
- `MapLevelsBus.RegisterMapsParent(Transform)` / `UnregisterMapsParent(Transform)` — bind scene container
- `MapLevelsSettings` — `unloadDistance`, `controllerTypeName` (resolved via `IMapLevelsController`)

### Output
- `MapLevelsBus.OnReady` (`InitValve`) — controller initialized
- `MapLevelsBus.OnRelease` — controller cleaned up
- `IMapLevelsController.OnActiveLevelChanged(guid, gateId)` — active level switched
- `IMapLevelsController.OnLevelLoaded(guid)` / `OnLevelUnloaded(guid)` — streaming events
- `MapLevelsModel.ActiveLevelGuid` — `StringData` for subscription

### Guarantees
- `Init` is idempotent: a repeated call on an initialized controller is a no-op
- `Enter` synchronously guarantees `Loaded` for the target level before changing `ActiveLevelGuid`
- Neighbor streaming is async fire-and-forget, checks `IsInitialized` and `ActiveLevelGuid` match
- `OnActiveLevelChanged` is not re-dispatched for the same `guid`
- On `OnLoadGame` the controller resets all instances and enters `CurrentLevelGuid`
- `MapLevelPreset.OnValidate` forces `RecordTypes.Singleton`

### Limits
- `MapLevelPreset` is fixed as `Singleton` — one runtime copy per preset
- The prefab is a direct reference (not Addressable) — all prefabs are bundled into the build
- Without a scene `MapsParent`, instances are stored in a hidden `MapVoid` (DontDestroyOnLoad)
- `controllerTypeName` resolution uses reflection across all assemblies — the type must have a public parameterless constructor

## Lifecycle Flow

1. `[RuntimeInitializeOnLoadMethod]` `MapLevelsBus.Bootstrap` subscribes controller creation to `Settings.OnInit`
2. `Settings.OnInit` → `CreateController` → reads `Settings.Data().MapLevels`, resolves type, instantiates via `Activator.CreateInstance`, calls `Init`
3. `Init` builds the `Catalog` from the DB, subscribes to `GameController.OnNewGame` / `OnLoadGame`, sets `IsInitialized = true`, calls `EnterCurrentFromGameData`
4. `Enter(guid)` → `EnsureLoaded` (synchronous instantiation) → `ActiveLevelGuid.Set` → `OnActiveLevelChanged` → `ApplyStreamingPlan` (async)
5. `ApplyStreamingPlan` — BFS, instantiate `hop=1`, unload `>= unloadDistance`
6. `App.OnExit` → `Dispose` (unsubscribe)

## Example: creating a level

```csharp
// 1. Create MapLevelPreset via menu Database/MapLevel Preset
//    Assign prefab and neighborGuids (through DbRecord selector)

// 2. On the prefab root — MapLevelView with UIStateSwitcher (SwitcherState.On/Off)
//    and optional MapGate array

// 3. In the scene — GameObject with MapsView (container for level instances)

// 4. Switching from project logic:
MapLevelsBus.OnReady.Subscribe(() =>
{
    MapLevelsBus.Controller.Enter(targetLevelGuid, gateId: "spawn_north");
});
```

## Example: replacing the controller

```csharp
// Project-side IMapLevelsController implementation
public sealed class MyMapLevelsController : Singleton<MyMapLevelsController>, IMapLevelsController { ... }

// In MapLevelsSettings → controllerTypeName pick via ValueSelector
// (the type provider scans the domain and filters by IMapLevelsController + parameterless ctor)
```

## Edge cases

- `controllerTypeName` empty → default `MapLevelsController` is used
- `controllerTypeName` does not resolve → `LogError`, controller is not created, `IsReady = false`
- `Enter` before `Init` → `LogError`, no-op
- `Enter` with unknown `guid` → `LogError`, no-op
- `MapsParent` already registered on a repeat attempt → `LogError`, `RegisterMapsParent` returns `false`
- `App.GetState() == AppStates.Stopping` during reparenting → instances are destroyed instead of moved
- `unloadDistance <= 1` → only the active level is retained; hop=1 neighbors are loaded but unloaded on the next `Enter`
