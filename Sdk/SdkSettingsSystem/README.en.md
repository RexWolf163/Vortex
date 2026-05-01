# SdkSettingsSystem

**Namespace:** `Vortex.Sdk.SdkSettingsSystem`
**Assembly:** `sdk.settings.system`

## Purpose

A single entry point for managing `Scripting Define Symbols` of pluggable Vortex SDK packages. Collects bool toggles from every Sdk package into one ScriptableObject asset and keeps them in sync with the project's PlayerSettings.

Capabilities:
- A single inspector asset with toggles to enable/disable each SDK package
- Automatic sync of defines on domain reload and target platform switch
- Two-way mode: "apply toggle state into PlayerSettings" and "read current defines back into toggles"
- Extension via `partial` + `asmref` — a new package adds one file, no central edits

Out of scope:
- Each package's own `defineConstraints` (live in their asmdef)
- Conditional compilation itself (handled by the C# compiler after sync)
- Cleanup of stale defines when a package is removed

## Dependencies

- `Vortex.Unity.CoreAssetsSystem` — `ICoreAsset`
- `Vortex.Unity.EditorTools` — `[ToggleButton]` attribute
- Sirenix Odin Inspector — `[Button]`, `[HorizontalGroup]`
- `UnityEditor` (Editor-only) — `PlayerSettings`, `EditorUserBuildSettings`, `AssetDatabase`

## Architecture

```
sdk.settings.system  (asmdef)
├── SdkSettings.cs            ← base partial, reflection engine
├── Attribute/
│   └── DefineSymbolAttribute.cs
└── Editor/
    └── MenuController.cs     ← Vortex/Configs/SDK Settings menu item

<package>/DefineSettings/
├── SdkSettings.<Package>.cs  ← partial chunk with one bool field
└── sdk.settings.system.ext.asmref → sdk.settings.system
```

The `asmref` ensures partial chunks are physically compiled into the same assembly as the base `SdkSettings`. Without it `partial` won't work (C# requirement).

### Components

| Class | Kind | Purpose |
|-------|------|---------|
| `SdkSettings` | `partial ScriptableObject`, `ICoreAsset` | Master asset; partial chunks contribute fields from each Sdk package |
| `DefineSymbolAttribute` | `Attribute` | Marker on a bool field: which `define` in PlayerSettings it controls |
| `MenuController` | static, Editor-only | `Vortex/Configs/SDK Settings` menu item to locate the asset |

## How to add a new Sdk package

1. In the new package's root, create a `DefineSettings/` folder.
2. Place two files inside:

   **`SdkSettings.<PackageName>.cs`:**
   ```csharp
   using UnityEngine;
   using Vortex.Sdk.SdkSettingsSystem.Attribute;
   using Vortex.Unity.EditorTools.Attributes;

   namespace Vortex.Sdk.SdkSettingsSystem
   {
       public partial class SdkSettings
       {
           [SerializeField, ToggleButton(isSingleButton: true)]
           [DefineSymbol("USING_VORTEX_MYPACKAGE")]
           private bool myPackageSdk = true;
       }
   }
   ```

   **`sdk.settings.system.ext.asmref`:**
   ```json
   { "reference": "GUID:56735331757685c47ba846af366ea373" }
   ```
   (The GUID corresponds to `sdk.settings.system.asmdef`.)

3. In the new package's asmdef set `defineConstraints: ["USING_VORTEX_MYPACKAGE"]` — the package will compile only when the toggle is on.

After project reimport a new toggle appears in the `SdkSettings` inspector.

## Contract

### Input
- bool fields in `partial SdkSettings` annotated with `[DefineSymbol("KEY")]`

### Output
- `PlayerSettings.SetScriptingDefineSymbols` — the synchronized set of defines for the current `BuildTargetGroup`

### Guarantees
- Every enabled field → its define is present in PlayerSettings
- Every disabled field → its define is absent
- A write to PlayerSettings happens only when the resulting set differs from the current one (idempotent)
- Duplicate `[DefineSymbol("X")]` → `LogError` and the second entry is skipped

### Constraints
- One `SdkSettings` asset per project (multiple → `LogError` + abort)
- All partial chunks must live in folders with an asmref to `sdk.settings.system`
- Editor-only: the engine does not ship into runtime builds

## Lifecycle

| Trigger | What happens |
|---------|-------------|
| First run after the asset is created (via `CoreAssetsController`) | `OnPlatformChanged` → asset has `_initialized = false` → `ReloadStates` (toggles are populated from the existing defines), then `_initialized = true` |
| `[InitializeOnLoadMethod]` (domain reload) on an already-initialized asset | `OnPlatformChanged` → `RefreshDefines` |
| `EditorUserBuildSettings.activeBuildTargetChanged` | Same — `RefreshDefines` |
| `ApplyChanges` button in the inspector | `RefreshDefines` — bool fields → PlayerSettings |
| `ReloadStates` button in the inspector | Read PlayerSettings → bool fields + `SetDirty` + `SaveAssets` |

### Why the `_initialized` flag

A freshly-created asset has all bool fields at their default values (usually `true`). Without the flag the very first `RefreshDefines` would overwrite the existing set of defines in PlayerSettings — for example, if the project already had its own defines with overlapping names before Vortex was added, or if the asset is created in a project where defines are already set via git. Therefore the first run is `ReloadStates` (read what's already in PlayerSettings into the toggles), and only after that does the normal sync kick in.

The flag is serialized in the asset (hidden from the inspector via `[HideInInspector]`).

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `SdkSettings` asset is absent | Silent no-op (`OnPlatformChanged` does nothing) |
| Multiple assets found | `LogError` and abort |
| Two partial chunks declare the same `[DefineSymbol("X")]` | `LogError` about the duplicate, only the first stays in the map |
| A `[DefineSymbol]`-marked field is not `bool` | Ignored |
| `BuildTargetGroup.Unknown` | Silent exit |
| `SaveAssets` failure inside `ReloadStates` | Swallowed (`try/catch{}`) — known simplification |
| Whole package deleted (partial chunk vanished) | The field disappears from the inspector, the define stays in PlayerSettings (zombie define) |

## Menu

`Vortex → Configs → SDK Settings` — locates the `SdkSettings` asset in the project and pings it via `MenuConfigSearchController`.
