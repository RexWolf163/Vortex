# DriverManagerSystem

**Namespace:** `Vortex.Unity.DriverManagerSystem`
**Assembly:** `ru.vortex.drivermanager`

## Purpose

Centralized configuration and validation of mappings between core systems (`AudioController`, `Database`, `SaveController`) and their platform-dependent drivers (`AudioDriver`, `DatabaseDriver`, `SaveSystemDriver`).

Capabilities:
- A "System → Driver" mapping table in a single ScriptableObject asset
- Auto-discovery of all `ISystemController` implementations and compatible drivers via reflection
- Code generation of a type-safe whitelist (`DriversGenericList.cs`)
- Configuration completeness validation before saving
- Project load check: warning on missing or out-of-sync `DriversGenericList.cs`

Out of scope:
- Driver creation and registration (implemented in specific systems)
- Runtime system initialization (handled by `Loader`)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `ISystemController`, `ISystemDriver` — system and driver interfaces |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — auto-created asset marker |
| `Vortex.Unity.EditorTools` | `[LabelText]` — Inspector attributes |
| `Vortex.Unity.Extensions` | `MenuConfigSearchController` — asset navigation |
| Odin Inspector | `[Button]`, `[InfoBox]`, `[ValueDropdown]`, `[ListDrawerSettings]` |

---

## Architecture

```
DriverManagerSystem/
├── Base/
│   ├── DriverConfig.cs                 # ScriptableObject — mapping table
│   └── DriverRecord.cs                 # Table row: system → driver
├── Editor/
│   └── MenuConfigSearchController.cs   # Menu: Vortex → Configs → Drivers Config
└── (generated) DriversGenericList.cs   # Auto-generated whitelist
```

### DriverConfig

ScriptableObject (`ICoreAsset`) storing a `DriverRecord[]` array. Located in `Assets/Resources/`, created automatically on first launch via `Vortex/Debug/Check Core Assets`.

Editor operations:
- **Reload** — scans assemblies, discovers all `ISystemController` implementations, updates the table (preserving existing bindings)
- **Save Config** — validates assignment completeness and generates `DriversGenericList.cs`

On project load (`InitializeOnLoadMethod`), checks for the presence and validity of `DriversGenericList.cs`.

### DriverRecord

Serializable table row. Stores `AssemblyQualifiedName` of the system and the selected driver. In Inspector displays:
- System name as label
- Dropdown of compatible drivers (filtered by interface via `GetDriverType()`)
- `[Switched Off]` option to disable a system

Compatibility detection: reflection invokes the static `GetDriverType()` method on the system to obtain the driver interface type, then finds all concrete implementations of that interface in `ru.vortex.*` assemblies.

### DriversGenericList.cs (auto-generated)

Static class in namespace `Vortex.Core.System`. Contains `Dictionary<string, string> WhiteList` — mapping system `AssemblyQualifiedName` to permitted driver `AssemblyQualifiedName`.

Used in `SystemController<T, TD>.SetDriver()` for runtime validation: only the driver specified in the whitelist can be connected.

---

## Contract

### Input
- A set of systems implementing `ISystemController`
- A set of drivers implementing corresponding `ISystemDriver` interfaces

### Output
- `DriversGenericList.cs` — compile-time whitelist for runtime validation

### Guarantees
- Cannot save configuration with empty assignments
- Cannot connect an incompatible driver at runtime
- Early error detection: log on project load if configuration is out of sync

---

## Usage

1. Open `DriverConfig` via menu **Vortex → Configs → Drivers Config**
2. Click **Reload** to discover all systems
3. Assign a driver to each system via dropdown
4. Click **Save Config** — `DriversGenericList.cs` is generated
5. On application start, drivers connect via `SystemController.SetDriver()` with whitelist validation

```csharp
// Create a system
public class MyService : SystemController<MyService, IMyDriver> { ... }

// Create a driver
public class MyServiceDriver : IMyDriver { ... }

// In DriverConfig: Reload → select MyServiceDriver → Save Config

// Runtime (inside driver):
MyService.SetDriver(this); // validated against DriversGenericList.WhiteList
```

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| New system added | Does not appear in table until Reload is clicked |
| Not all drivers assigned | Save Config rejected with `LogError` |
| `DriversGenericList.cs` deleted | Warning on project load |
| `DriversGenericList.cs` out of sync with DriverConfig | Warning on project load |
| `SetDriver()` with unauthorized driver | Runtime validation error |
| `onlyInVortexSearch` flag disabled | Driver search across all project assemblies |
| System set to `[Switched Off]` | No driver assigned, system not initialized |
