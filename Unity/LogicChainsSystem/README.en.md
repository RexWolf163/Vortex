# LogicChainsSystem (Unity)

**Namespace:** `Vortex.Unity.LogicChainsSystem.*`
**Assembly:** `ru.vortex.unity.logicchains`
**Platform:** Unity 2021.3+

---

## Purpose

Unity layer of the logic chain system. Provides ScriptableObject presets for visual chain configuration in Inspector, base actions and conditions, and a component for launching chains from scene.

Capabilities:

- `LogicChainPreset` — ScriptableObject chain preset (`Database/Logic Chain`)
- `ChainStepPreset`, `ConnectorPreset` — step and transition setup in Inspector
- `UnityLogicAction` — base class for Unity actions with `[ClassLabel]`
- `LoadScene` — built-in scene loading action
- `LogicChainStarter` — MonoBehaviour for launching chains on `Database.OnInit`
- Unity conditions (separate assembly `ru.vortex.unity.logicconditions`)

Out of scope:

- Chain execution logic — Core (`LogicChains`)
- Models `ChainStep`, `Connector`, `LogicAction`, `Condition` — Core

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LogicChainsSystem` | `LogicChains`, `LogicChain`, `ChainStep`, `Connector`, `LogicAction`, `Condition` |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database`, `RecordTypes` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `ObjectExtCopy`, `StringExtensions` |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>`, `DbRecordAttribute` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` for collection element display |
| `Vortex.Unity.AppSystem` | `TimeController` (for `LoadScene`) |
| Odin Inspector | `[ValueDropdown]`, `[HideReferenceObjectPicker]`, `[SerializeReference]` |

---

## Architecture

```
LogicChainPreset : RecordPreset<LogicChain>  (ScriptableObject)
  ├── startStep: string (GUID)
  ├── chainSteps: ChainStepPreset[]
  ├── ChainSteps → Dictionary<string, ChainStep>  ← conversion via CopyFrom
  └── Editor: GetStepsList(), TestStartStep(), OnValidate()

ChainStepPreset [Serializable, ClassLabel]
  ├── guid, name, description
  ├── actions: LogicAction[]               ← [SerializeReference]
  ├── connectors: ConnectorPreset[]        ← [SerializeReference]
  └── Editor: EditorInit(owner), GetStepName()

ConnectorPreset [Serializable, ClassLabel]
  ├── targetStepGuid: string               ← [ValueDropdown] from chain steps
  ├── conditions: Condition[]              ← [SerializeReference]
  └── Editor: GetTargets(), GetConnectorName()

UnityLogicAction : LogicAction
  └── abstract NameAction → [ClassLabel("@NameAction")]

LoadScene : UnityLogicAction
  ├── SceneName                            ← [ValueDropdown] from Build Settings
  ├── _additiveMode: bool
  └── _async: bool                         ← default true

UnLoadScene : UnityLogicAction
  ├── SceneName                            ← [ValueDropdown] from Build Settings
  └── _async: bool                         ← default true

LogicChainStarter : MonoBehaviour
  ├── logicChain: string                   ← [DbRecord(LogicChain, MultiInstance)]
  └── Start → Database.OnInit += CallChain
```

### Preset → Runtime Conversion

`LogicChainPreset` stores `ChainStepPreset[]` in Inspector. When `ChainSteps` is accessed, each `ChainStepPreset` is converted to `ChainStep` via `ObjectExtCopy.CopyFrom`. `ConnectorPreset` is similarly converted to `Connector`. This ensures multi-instance — each `Database.GetNewRecord<LogicChain>` call creates an independent copy.

### Inspector Integration

- `ChainStepPreset` — `[ClassLabel("@GetStepName()")]` displays step name in collection
- `ConnectorPreset` — `[ClassLabel("@GetConnectorName()")]` displays transition target: `"to «StepName»"`, `"Complete this chain"`, or `"Empty Connector"`
- `startStep` — `[ValueDropdown]` from step list, red highlight on invalid GUID
- `targetStepGuid` — `[ValueDropdown]` from chain steps (excluding current) + `"_CompleteChain"`
- `LogicAction[]` and `Condition[]` — `[SerializeReference, HideReferenceObjectPicker]` for polymorphism

---

## Element distribution across packages

Actions (`LogicAction`) and conditions (`Condition`) are not concentrated in a single package — **each package contributes its own chain elements**, next to the functionality they exercise. They appear in the `[SerializeReference]` inspector dropdowns automatically through Odin type-scanning (no manual registration). This is the same `package-composition-first` principle used across the rest of the framework (see `COMPOSITION.md`).

To use an element, its package just needs to be present in the project — removing the package drops its elements from the dropdowns while the rest of the chain still compiles.

| Element | Kind | Package | Purpose |
|---------|------|---------|---------|
| `LoadScene` | Action | `ru.vortex.unity.logicchains` | Load a scene (single/additive, sync/async) |
| `UnLoadScene` | Action | `ru.vortex.unity.logicchains` | Unload a scene (sync/async) |
| `SceneLoaded` | Condition | `ru.vortex.unity.logicconditions` | Scene is loaded (sees Additive scenes too) |
| `SystemsLoaded` | Condition | `ru.vortex.unity.logicconditions` | `App.GetState() == Running` (Vortex systems ready) |
| `MinTimeCondition` | Condition | `ru.vortex.unity.logicconditions` | ≥ N seconds elapsed (minimum step duration) |
| `OpenUI` | Action | `ru.vortex.unity.uiprovider` | Open an interface via `UIProvider.Open` |
| `CloseUI` | Action | `ru.vortex.unity.uiprovider` | Close an interface via `UIProvider.Close` |
| `CloseAllUI` | Action | `ru.vortex.unity.uiprovider` | Close all common interfaces |
| `NaninovelInitialized` | Condition | `ru.vortex.nani.core` | Naninovel engine initialised (`Engine.Initialized`) |

**Reentrancy convention.** Actions that touch scenes or UI (`LoadScene`, `UnLoadScene`, `OpenUI`, `CloseUI`, `CloseAllUI`) defer the real work to the end of the frame via `TimeController.Call`/`Accumulate`. The reason: `Invoke()` is called from the chain-advance stack (`RunChain → CheckConditions → RunChain`), and a synchronous scene load/UI close right inside that call would re-enter. A custom action that changes scenes/UI should follow the same pattern.

---

## Conditions (LogicConditionsSystem)

Separate assembly `ru.vortex.unity.logicconditions`. Base class `UnityCondition : Condition` with `[ClassLabel("@ConditionName")]`.

| Condition | Description | Check |
|-----------|-------------|-------|
| `SceneLoaded` | Waits for a scene to load by name | `GetSceneByName(name).IsValid() && isLoaded` — sees Additive scenes too, not only the active one; subscribes to `SceneManager.sceneLoaded` |
| `SystemsLoaded` | Waits for `App.GetState() == Running` | Subscribes to `App.OnStateChanged` |
| `MinTimeCondition` | Minimum wait time (seconds) | `DateTime.UtcNow >= target` via `TimeController` |

All conditions follow the pattern: check in `Start()` → if already fulfilled, `RunCallback()` immediately; otherwise subscribe to event.

`NaninovelInitialized` (the Naninovel engine readiness condition) lives in the `ru.vortex.nani.core` package — see the "Element distribution across packages" table.

---

## Contract

### Input

- `LogicChainPreset` created via `Create > Database > Logic Chain`
- Steps, actions, conditions configured in Inspector
- Launch: `LogicChainStarter` on scene or `LogicChains.AddChain(presetGuid)` from code

### Output

- Chain executes according to Core logic: steps → actions → conditions → transitions

### API

| Component | Purpose |
|-----------|---------|
| `LogicChainPreset` | ScriptableObject, created via `Database/Logic Chain` |
| `LogicChainStarter` | MonoBehaviour, launches chain on `Database.OnInit` |
| `UnityLogicAction` | Base class for Unity actions |
| `UnityCondition` | Base class for Unity conditions |

### Built-in Actions

| Action | Description |
|--------|-------------|
| `LoadScene` | Scene loading (sync/async, single/additive) via `TimeController.Call` |
| `UnLoadScene` | Scene unloading (sync/async) via `TimeController.Call` |

For the full list of actions and conditions across all packages, see "Element distribution across packages".

### Constraints

| Constraint | Reason |
|------------|--------|
| `LogicChainStarter` triggers on `Database.OnInit` | Requires Database initialization |
| `LoadScene` executes via `TimeController.Call` | Guarantees main thread execution |
| Actions and conditions are `[SerializeReference]` | Polymorphism, but no drag & drop assets |
| Step GUIDs generated on creation | `Crypto.GetNewGuid()` in field initializer |

---

## Usage

### Creating a chain

1. `Create > Database > Logic Chain` — create preset
2. Add steps (`ChainStepPreset[]`) with names and descriptions
3. In each step, add actions (`LogicAction[]`) and connectors (`ConnectorPreset[]`)
4. In connectors, specify transition target and conditions
5. Set `startStep` — initial step

### Launching from scene

Add `LogicChainStarter` to a GameObject, select chain preset via `[DbRecord]` field.

---

### Example: app loading through a chain

The canonical app-startup scenario — a loader chain that waits for the systems to be ready, loads the main scene, and removes the loading screen.

#### Two layout variants

**Classic (without Naninovel).** `Preloader` (the loading screen) is the start scene itself. This is simpler: you don't need a separate object to load it — it is already open at startup. The chain loads `Main` additively and just unloads `Preloader` at the end:

```
Preloader (start scene = loading screen)
└── [Autorun]
    ├── LoaderStarter            (kicks off Loader → Vortex systems loading)
    └── LogicChainStarter        (Logic Chain: LoaderChain)
```
Chain: wait for systems → load `Main` (Additive) → unload `Preloader`.

**With Naninovel.** Naninovel is finicky about **unloading the start scene** — you cannot cleanly unload the scene that was the very first one. So a thin service scene `Loading` is made the start scene, and it additively loads `Preloader`. Now `Preloader` is **not** the start scene and can be safely unloaded at the end of the chain. That is the layout shown below (and in the first screenshot).

#### Scene and `[Autorun]` components (the Naninovel variant)

```
Loading (thin start scene)
└── [Autorun]
    ├── LoadSceneHandler         (Scene Name: Preloader, Additive Mode ✓)
    │       └── loads the loading screen additively (as a separate, non-start scene)
    ├── LoaderStarter            (kicks off Loader → Vortex systems loading)
    ├── LogicChainStarter        (Logic Chain: LoaderChain)
    └── MonoBehaviourEventsHandler
            └── On Enable → LoadSceneHandler.Run()
```

Component roles:
- **`LoaderStarter`** — on `App.OnStarting`, calls `Loader.Run()`: starts the asynchronous loading of every framework system. When done, `App` transitions to `Running`.
- **`LoadSceneHandler`** (from `Unity/Components/SceneControllers`) — loads the `Preloader` loading-screen scene additively. Invoked from `MonoBehaviourEventsHandler.OnEnable`.
- **`LogicChainStarter`** — on `Database.OnInit`, instantiates the `LoaderChain` preset and runs it.

#### `LoaderChain` preset (Logic Chain, Multi Instance)

Three steps, `Start Step = "Waiting Loading"`:

```
1. Waiting Loading  (description: "Systems loading")
   Actions:    — (empty, the step only waits)
   Connector → LoadScene
      Conditions (AND):
        • SystemsLoaded         → "Wait all systems loading"
        • NaninovelInitialized  → "Wait Naninovel initialization"

2. LoadScene
   Actions:
        • LoadScene(Main, Additive ✓, Async ✓)  → "Call load for «Main» scene"
   Connector → Hide Loader UI
      Conditions:
        • SceneLoaded(Main)     → "Wait Main loading"

3. Hide Loader UI  (description: "hide the loading UI")
   Actions:
        • UnLoadScene(Preloader, Async ✓)        → "Call unload for «Preloader» scene"
   Connector → _CompleteChain
      Conditions: — (empty → automatic transition → the chain completes and is removed)
```

#### Step-by-step

1. The `Loading` scene starts. `[Autorun].OnEnable` → `LoadSceneHandler.Run()` loads `Preloader` (the loading screen) on top. In parallel `LoaderStarter` starts systems loading and `LogicChainStarter` runs `LoaderChain`.
2. **Step "Waiting Loading"** has no actions — it waits until **both** conditions on a single connector are met: Vortex systems are up (`SystemsLoaded` → `App.Running`) **and** Naninovel has initialised (`NaninovelInitialized`). Two conditions on one connector form a conjunction — the transition happens only when both subsystems are ready, regardless of which finishes first.
3. **Step "LoadScene"** — the `LoadScene` action loads the main `Main` scene additively and asynchronously. The connector waits for `SceneLoaded(Main)` — the event fires for the loaded scene (including Additive) matched by name.
4. **Step "Hide Loader UI"** — the `UnLoadScene` action unloads `Preloader` (removes the loading screen). The connector has no conditions → an automatic transition to `_CompleteChain` → the chain completes and is removed from the registry.

The result: the loading screen stays from start until `Main` is fully loaded, then is removed by a single scene unload. No orchestrator code — the order and conditions are declared in the preset.

> 💡 If the loading screen is a `UIProvider` interface rather than a separate scene, the final step uses `CloseUI`/`CloseAllUI` (package `ru.vortex.unity.uiprovider`) instead of `UnLoadScene(Preloader)`.

---

### Creating a custom action

```csharp
public class PlaySound : UnityLogicAction
{
    [SerializeField] private AudioClip clip;

    public override void Invoke()
    {
        AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }

    protected override string NameAction => $"Play «{(clip ? clip.name : "?")}»";
}
```

### Creating a custom condition

```csharp
public class ButtonClicked : UnityCondition
{
    [SerializeField] private string buttonId;

    protected override void Start()
    {
        UIEvents.OnButtonClick += OnClick;
    }

    private void OnClick(string id)
    {
        if (id == buttonId) RunCallback();
    }

    public override bool Check() => UIEvents.LastClickedButton == buttonId;
    public override void DeInit() => UIEvents.OnButtonClick -= OnClick;

    protected override string ConditionName => $"Wait click «{buttonId}»";
}
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `startStep` not set or invalid | Red highlight in Inspector, error on `RunChain` |
| Connector without target | `"Empty Connector"` in Inspector, error on transition |
| `LogicChainStarter` before Database init | Subscribes to `Database.OnInit`, launch deferred |
| Multiple connectors without conditions | First one executes, others ignored |
| Multiple conditions on a single connector | Conjunction (AND) — the transition happens only when `Check()` of every condition returned true, regardless of completion order |
| `LoadScene` / `UnLoadScene` with `_async = false` | Synchronous load/unload, possible frame freeze |
| Step without actions | Allowed — proceeds directly to connector condition checks |
| Step without connectors | Chain stops at this step permanently |
