# StateAxisSystem (Unity)

**Namespace:** `Vortex.Unity.StateAxisSystem.*`
**Assemblies:** `ru.vortex.unity.stateaxis` (runtime), `ru.vortex.unity.stateaxis.editor` (Editor-only)

---

## Purpose

Unity-side wrapper around the `StateAxis` abstraction (Layer 1):

- Paired asset `StateAxisPreset` — source of truth for codegen.
- Generates `.cs` next to the asset on the **Save** button in the custom inspector.
- Generated file lifecycle: rename → old file deleted, preset deleted → paired `.cs` removed.
- Forced initialization of all `StateAxis` subclasses at startup (runtime + Editor).
- Inspector attribute `[StateKey(typeof(MoveState))]` with a dropdown of valid keys.
- `StateValueSwitcherHandler` bridge from `StateValue<T>` to `UIStateSwitcher`.
- Editor validation that preset and generated class are in sync, run on entering Play Mode.
- Window for finding orphan `.cs` files (no paired preset).

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.StateAxisSystem` | `StateAxis`, `StateValue<T>` |
| `Vortex.Core.Extensions.ReactiveValues` | `IReactiveData` for subscription in `StateValueSwitcherHandler` |
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` for the bridge |
| Sirenix Odin Inspector | `[ValueDropdown]` in `StateValueSwitcherHandler` |

---

## Workflow

### Creating an axis

1. **Project window → Create → Vortex → StateAxis Preset.**
   A `.asset` is created in the chosen folder.
2. In the preset inspector fill in:
   - **Axis Name** — class name, PascalCase, valid C# identifier (e.g., `MoveState`).
   - **Target Namespace** — class namespace (e.g., `MyGame.States`).
   - **Keys** — ordered list of keys (e.g., `Idle`, `Walk`, `Run`, `Jump`).
3. **Press Save.**
   This generates `{asset_folder}/{AxisName}.cs` containing the class, static `readonly` instances,
   the `All` property, and a nested editor menu `Vortex/StateAxis/{AxisName}` to quickly open the preset.

### Editing an axis

1. Find the preset via the **Vortex/StateAxis/{AxisName}** menu (auto-added by every generated class)
   or manually in the Project window.
2. Edit fields → **Save**.
3. If **Axis Name** changed — old `.cs` is deleted, new one is created.

### Restoring a preset from code (Load)

If the preset is lost but the `.cs` exists:
1. Create a new preset.
2. Set the same Axis Name and Namespace as in the existing class.
3. **Press Load.** Reflects the current class and writes its keys into the preset.
4. Persist `lastGeneratedPath` via **Save** (regenerates the .cs identically in the process).

### Removing an axis

Deleting the preset via the Project window triggers `StateAxisAssetPostprocessor.OnWillDeleteAsset`
which automatically removes the paired `.cs`.

If the `.cs` is left over or removed without the preset — use
**Tools → Vortex → StateAxis → Find orphans** for cleanup.

---

## Architecture

### Runtime

```
Presets/
  StateAxisPreset                — ScriptableObject: AxisName, Namespace, Keys[], LastGeneratedPath

Initialization/
  StateAxisInitializer            — [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] +
                                    [InitializeOnLoadMethod] (Editor) — RunClassConstructor for
                                    every non-abstract StateAxis subclass

Attributes/
  StateKeyAttribute               — [StateKey(typeof(TAxis))] for string fields in the Inspector

Handlers/
  StateValueSwitcherHandler       — MonoBehaviour: source + property name + UIStateSwitcher;
                                    subscribes to IReactiveData.OnUpdateData,
                                    calls switcher.Set(stateValue.Index) on change
```

### Editor

```
StateAxisCodeGenerator            — Generate(name, ns, keys) → .cs string; name validation
StateAxisPresetEditor             — Custom inspector with Save/Load and live dot-stripping
StateAxisAssetPostprocessor       — OnWillDeleteAsset → delete paired .cs
StateAxisOrphanFinder             — "Find orphans" window
StateAxisValidator                — [InitializeOnLoad], hook on playModeStateChanged → ValidateAll
                                    + menu Tools/Vortex/StateAxis/Validate all presets
StateKeyAttributeDrawer           — popup with axis keys for [StateKey]
```

### Generation flow

```
Save in Inspector
  ↓
Validate (names, duplicates, empty keys)
  ↓
If LastGeneratedPath ≠ {folder}/{AxisName}.cs → DeleteAsset(old)
  ↓
StateAxisCodeGenerator.Generate(...) → string
  ↓
File.WriteAllText("{folder}/{AxisName}.cs", content)
  ↓
preset.lastGeneratedPath = new path
  ↓
AssetDatabase.SaveAssets() + Refresh() → recompile
  ↓
Class static initializers → registration in StateAxis registry
```

### Validation flow (entering Play Mode)

```
EditorApplication.playModeStateChanged → ExitingEditMode
  ↓
All StateAxisPreset assets in the project
  ↓
For each: resolve type by {Namespace}.{AxisName}
  ↓
RuntimeHelpers.RunClassConstructor → registry populated
  ↓
Compare preset.Keys vs StateAxis.GetAll(type).Select(s => s.Key)
  ↓
Mismatch → Debug.LogError(preset, "what's missing on each side")
```

Validation **does not block** entering Play — that decision is up to the developer.

---

## Generated class template

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//   This file was generated by Vortex StateAxis Code Generator.
//   DO NOT EDIT MANUALLY. Any manual changes will be lost on the next regeneration.
//   To change the value set, edit the paired preset (MoveState.asset) in the
//   inspector and press Save.
// </auto-generated>
//------------------------------------------------------------------------------

using System.Collections.Generic;
using Vortex.Core.StateAxisSystem.Abstractions;

namespace MyGame.States
{
    public sealed class MoveState : StateAxis
    {
        public static readonly MoveState Idle = new(nameof(Idle), 0);
        public static readonly MoveState Walk = new(nameof(Walk), 1);
        public static readonly MoveState Run  = new(nameof(Run),  2);
        public static readonly MoveState Jump = new(nameof(Jump), 3);

        public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

        private MoveState(string key, int order) : base(key, order) { }

#if UNITY_EDITOR
        private static class EditorMenu
        {
            [UnityEditor.MenuItem("Vortex/StateAxis/MoveState")]
            private static void OpenPreset()
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("MoveState t:StateAxisPreset");
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<Vortex.Unity.StateAxisSystem.Presets.StateAxisPreset>(path);
                    if (preset != null && preset.AxisName == "MoveState")
                    {
                        UnityEditor.Selection.activeObject = preset;
                        UnityEditor.EditorGUIUtility.PingObject(preset);
                        return;
                    }
                }
            }
        }
#endif
    }
}
```

The `Vortex/StateAxis/{AxisName}` menu appears automatically on recompilation —
no special hacks with `Menu.AddMenuItem`.

---

## `[StateKey]` Inspector attribute

```csharp
public class CharacterPreset : ScriptableObject
{
    [StateKey(typeof(MoveState))]
    [SerializeField] private string defaultMoveState;

    public MoveState GetDefault() => StateAxis.GetByKey<MoveState>(defaultMoveState);
}
```

The drawer shows a popup of keys read from `StateAxis.GetAll(typeof(MoveState))`.
Before reading the registry it calls `RuntimeHelpers.RunClassConstructor`, so the dropdown
works in Editor mode immediately on Inspector open, without entering Play.

---

## `StateValueSwitcherHandler` — bridge

Inspector configuration:
- **source** — MonoBehaviour exposing a property of type `StateValue<TAxis>`.
- **property** — name of that property (dropdown filters by type).
- **switcher** — target `UIStateSwitcher`.

On value change, the subscription on `IReactiveData.OnUpdateData` calls
`switcher.Set(stateValue.Index)`. The link between key and switcher slot is the axis `Order`
defined in the preset. The switcher's slots must be in the same order as the keys in the preset.

---

## Edge cases

| Scenario | Behavior |
|----------|----------|
| Save with empty Axis Name | Validation dialog; file not generated |
| Save with duplicate keys | Validation dialog; file not generated |
| Save after AxisName rename | Old `.cs` deleted, new generated, `lastGeneratedPath` updated |
| Save on an unsaved (in-memory) preset | Dialog "Preset is not saved in the project" |
| Load before first Save | Dialog "Type not found. Save first" |
| Load for a type that doesn't inherit StateAxis | Dialog "doesn't inherit StateAxis" |
| Deleting the preset | `.cs` deleted automatically via AssetPostprocessor |
| Deleting the `.cs` manually (without preset) | Validator on Play logs error: "type not found" |
| Preset ↔ class mismatch | Validator on Play prints both diffs, doesn't block entering Play |
| Find orphans with no presets | Window shows "no orphans" |
| `[StateKey]` for an axis with no values | Popup empty, HelpBox "Save the preset" |
| `StateValueSwitcherHandler` with source/property unset | Awake logs error, `enabled = false` |

---

## Public API

```csharp
// Runtime
namespace Vortex.Unity.StateAxisSystem.Presets
{
    public class StateAxisPreset : ScriptableObject
    {
        public string AxisName { get; }
        public string TargetNamespace { get; }
        public IReadOnlyList<string> Keys { get; }
        public string LastGeneratedPath { get; }
    }
}

namespace Vortex.Unity.StateAxisSystem.Initialization
{
    public static class StateAxisInitializer
    {
        public static void Initialize();
    }
}

namespace Vortex.Unity.StateAxisSystem.Attributes
{
    public class StateKeyAttribute : PropertyAttribute
    {
        public Type AxisType { get; }
        public StateKeyAttribute(Type axisType);
    }
}

namespace Vortex.Unity.StateAxisSystem.Handlers
{
    public class StateValueSwitcherHandler : MonoBehaviour { }
}
```

Editor classes (`StateAxisCodeGenerator`, `StateAxisPresetEditor`, …) are internal.
