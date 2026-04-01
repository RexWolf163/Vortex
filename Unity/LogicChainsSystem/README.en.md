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

## Conditions (LogicConditionsSystem)

Separate assembly `ru.vortex.unity.logicconditions`. Base class `UnityCondition : Condition` with `[ClassLabel("@ConditionName")]`.

| Condition | Description | Check |
|-----------|-------------|-------|
| `SceneLoaded` | Waits for scene to load | `SceneManager.GetActiveScene().name == SceneName` |
| `SystemsLoaded` | Waits for `App.GetState() == Running` | Subscribes to `App.OnStateChanged` |
| `MinTimeCondition` | Minimum wait time (seconds) | `DateTime.UtcNow >= target` via `TimeController` |

All conditions follow the pattern: check in `Start()` → if already fulfilled, `RunCallback()` immediately; otherwise subscribe to event.

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
| `LoadScene` with `_async = false` | Synchronous load, possible frame freeze |
| Step without actions | Allowed — proceeds directly to connector condition checks |
| Step without connectors | Chain stops at this step permanently |
