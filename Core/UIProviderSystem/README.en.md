# UIProviderSystem (Core)

**Namespace:** `Vortex.Core.UIProviderSystem.Bus`, `Vortex.Core.UIProviderSystem.Model`, `Vortex.Core.UIProviderSystem.Enums`
**Assembly:** `ru.vortex.uiprovider`
**Platform:** .NET Standard 2.1+

---

## Purpose

Bus for managing user interface lifecycle. Registers interfaces by GUID, manages their state (open/closed), and provides automatic open/close through declarative conditions.

Conceptually, an interface is treated as an empty container with no logic of its own. Subclassing `UserInterface` is not intended. Interface behavior is entirely composed from autonomous components: conditions (`UserInterfaceCondition`) determine when to open/close, type (`UserInterfaceTypes`) controls participation in bulk operations, drag offset handles positioning. Logic specific to a particular interface is implemented by external components, not inside the container.

Capabilities:

- `UIProvider` — static bus: `Open()`, `Close()`, `CloseAll()`, `Register()`, `Unregister()`
- `UserInterfaceData` — interface data model (`Record`) with conditions, events, and drag offset
- `UserInterfaceCondition` — abstract condition with callback monitoring
- `ConditionAnswer` — check result: `Idle`, `Open`, `Close`
- `UserInterfaceTypes` — classification: `Common`, `Panel`, `Overlay`, `Popup`
- Event model: `OnOpen`/`OnClose` on bus and on each `UserInterfaceData`
- Window position saving via `Record.GetDataForSave()`/`LoadFromSaveData()`

Out of scope:

- ScriptableObject presets, MonoBehaviour views — Unity layer
- Open/close animations (TweenerHub) — Unity layer
- Window dragging (drag handler) — Unity layer
- Condition code generation — Unity layer

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetRecord()` |
| `Vortex.Core.AppSystem` | `App.GetState()` (check on registration) |
| `Vortex.Core.SettingsSystem` | `Settings.Data().AppStateDebugMode` (debug log) |
| `Vortex.Core.LoggerSystem` | `Log.Print()` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `DictionaryExt.Get()` |

---

## Architecture

```
UIProvider (static partial class)
  ├── Uis: Dictionary<string, UserInterfaceData>
  │
  ├── Register(id) → UserInterfaceData       ← Database.GetRecord, debug log
  ├── Unregister(id)
  ├── Open(id)                                ← data.Open()
  ├── Close(id)                               ← data.Close()
  ├── CloseAll()                              ← Common only
  ├── HasOpenedUIs() → bool                   ← Common only
  ├── GetOpenedUIs() → UserInterfaceData[]    ← Common only
  ├── OnOpen: Action (event)
  └── OnClose: Action (event)

UserInterfaceData : Record
  ├── IsOpen: bool
  ├── UIType: UserInterfaceTypes
  ├── Conditions: UserInterfaceCondition[]
  ├── Offset: (int x, int y)                 ← window position
  │
  ├── OnOpen: Action                          ← safe subscribe: if IsOpen — fires immediately
  ├── OnClose: Action                         ← safe subscribe: if !IsOpen — fires immediately
  │
  ├── Init() → foreach Condition.Init(this, CheckConditions)
  ├── DeInit() → foreach Condition.DeInit()
  ├── Open() → IsOpen=true, OnOpen, UIProvider.CallOnOpen
  ├── Close() → IsOpen=false, OnClose, UIProvider.CallOnClose
  ├── CheckConditions()                       ← invoked by condition callback
  │
  ├── GetDataForSave() → "x;y"
  └── LoadFromSaveData(data) → Offset

UserInterfaceCondition (abstract, Serializable)
  ├── Data: UserInterfaceData (protected)
  ├── Init(data, callback) → Run()
  ├── RunCallback() → callback
  ├── Run() (abstract)                        ← subscribe to events
  ├── DeInit() (abstract)                     ← unsubscribe
  └── Check() → ConditionAnswer (abstract)

ConditionAnswer
  ├── Idle                                    ← no effect
  ├── Open                                    ← requires open
  └── Close                                   ← requires close

UserInterfaceTypes
  ├── Common                                  ← base windows (included in CloseAll, GetOpenedUIs)
  ├── Panel
  ├── Overlay
  └── Popup
```

### Condition Mechanism

1. `Init()` calls `Condition.Init(data, CheckConditions)` for each condition
2. Condition subscribes to external events in `Run()` and calls `RunCallback()` on change
3. `RunCallback()` triggers `CheckConditions()` — checks all conditions
4. `CheckConditions()`: initial state = current (`IsOpen`). For each condition:
   - `Idle` — skip
   - `Open` — `state = Open`
   - `Close` (or exception) → immediate `Close()`, return
5. Final `state`: `Open` → `Open()`, otherwise → `Close()`
6. Priority: **Close wins** — any condition returning `Close` immediately closes the UI

### Safe Subscribe on OnOpen/OnClose

`UserInterfaceData.OnOpen` is a custom event accessor. On subscription, if `IsOpen == true`, the callback fires immediately. Similarly, `OnClose` — on subscription, if `!IsOpen`, the callback fires immediately. Pattern matches `SystemController.OnInit`.

### Type Classification

`CloseAll()`, `HasOpenedUIs()`, `GetOpenedUIs()` filter only `Common`. Types `Panel`, `Overlay`, `Popup` are unaffected by bulk operations.

---

## Contract

### Input

- `Register(id)` — gets `UserInterfaceData` from `Database`, adds to index
- Conditions (`UserInterfaceCondition[]`) set via preset in Unity layer
- `Open(id)` / `Close(id)` — manual control

### Output

- `IsOpen` on `UserInterfaceData` — current state
- Events: `OnOpen` / `OnClose` on data and bus
- `GetOpenedUIs()` — array of open Common interfaces
- `GetDataForSave()` — string `"x;y"` for position saving

### API

| Method | Description |
|--------|-------------|
| `UIProvider.Register(id)` | Register by preset GUID from Database |
| `UIProvider.Unregister(id)` | Unregister |
| `UIProvider.Open(id)` | Open interface |
| `UIProvider.Close(id)` | Close interface |
| `UIProvider.CloseAll()` | Close all Common |
| `UIProvider.HasOpenedUIs()` | Any open Common interfaces |
| `UIProvider.GetOpenedUIs()` | Array of open Common |

### Constraints

| Constraint | Reason |
|------------|--------|
| `Register` requires `App.GetState() >= Starting` | Depends on App initialization |
| `CloseAll` / `GetOpenedUIs` — Common only | Panels, overlays, popups unaffected |
| `Close` takes priority over `Open` in conditions | First `Close` aborts checking |
| Position is `(int, int)` | Integer pixels |
| Preset GUID must be unique | Duplicates overwrite index entry |

---

## Usage

### Creating a condition

```csharp
[Serializable]
public class PlayerDeadCondition : UserInterfaceCondition
{
    protected override void Run()
    {
        PlayerModel.OnDeath += RunCallback;
        PlayerModel.OnRespawn += RunCallback;
        RunCallback();
    }

    public override void DeInit()
    {
        PlayerModel.OnDeath -= RunCallback;
        PlayerModel.OnRespawn -= RunCallback;
    }

    public override ConditionAnswer Check()
    {
        return PlayerModel.IsDead ? ConditionAnswer.Open : ConditionAnswer.Close;
    }
}
```

### Manual control

```csharp
UIProvider.Open("settings-menu-guid");
UIProvider.Close("settings-menu-guid");
UIProvider.CloseAll();

var openUIs = UIProvider.GetOpenedUIs();
```

### Event subscription

```csharp
// Safe subscribe — if UI is already open, callback fires immediately
uiData.OnOpen += () => Debug.Log("Opened");
uiData.OnClose += () => Debug.Log("Closed");
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `Open` on already open UI | No-op (`IsOpen` check) |
| `Close` on already closed UI | No-op |
| GUID not found in index | `Log.Print(Error)`, no-op |
| `Register` when `App.GetState() < Starting` | Returns `null` |
| Condition throws exception in `Check()` | `Debug.LogException`, immediate `Close()` |
| All conditions return `Idle` | State unchanged (remains current) |
| Subscribe to `OnOpen` when `IsOpen == true` | Callback fires immediately |
| Subscribe to `OnClose` when `IsOpen == false` | Callback fires immediately |
| `AppStateDebugMode` active | Log on Register/Unregister |
