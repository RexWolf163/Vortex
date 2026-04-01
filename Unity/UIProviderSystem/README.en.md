# UIProviderSystem (Unity)

**Namespace:** `Vortex.Unity.UIProviderSystem`
**Assembly:** `ru.vortex.unity.uiprovider`
**Platform:** Unity 2021.3+

---

## Purpose

Unity adaptation of the `UIProvider` bus. Binds Core models to MonoBehaviour views, provides animated open/close via TweenerHub, window dragging with screen bounds clamping, and declarative control through ScriptableObject presets.

An interface is treated as an empty container with no logic of its own. The `UserInterface` component is used as-is — subclassing is not intended. All behavior is composed from autonomous components: conditions in the preset control visibility, `TweenerHub` handles animation, `UIDragHandler` handles dragging, `CallUIHandler` handles manual invocation. Logic specific to a particular window is implemented by external components on the same object, not inside `UserInterface`.

Capabilities:

- `UserInterface` — MonoBehaviour view: registration, animated open/close via `TweenerHub[]`, drag support
- `UserInterfacePreset` — ScriptableObject preset: `RecordPreset<UserInterfaceData>`, type and conditions
- `CallUIHandler` / `CallUIClose` — button handlers for open/close/toggle
- `UIDragHandler` — window dragging via `IDragHandler` with `CanvasScaler` binding
- `UnityUserInterfaceCondition` — abstract Unity condition with `DisplayAsString` for Inspector
- Built-in conditions: `AutoLoadCondition`, `CloseOnOpenAnyUICondition`, `SaveLoadStartCondition`, `OrCondition`
- Condition code generation via `Create > Vortex Templates > UI Condition`

Out of scope:

- Platform-independent bus, models, condition checking — Core layer (`Vortex.Core.UIProviderSystem`)
- UI layout and visual styling
- Input handling logic within interfaces

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.UIProviderSystem` | `UIProvider`, `UserInterfaceData`, `UserInterfaceCondition`, `ConditionAnswer` |
| `Vortex.Core.DatabaseSystem` | `RecordPreset<T>`, `Database.GetRecord()` |
| `Vortex.Core.AppSystem` | `App.OnStart` (registration timing) |
| `Vortex.Core.SaveSystem` | `SaveController` (`SaveLoadStartCondition` condition) |
| `Vortex.Unity.AppSystem` | `TimeController.Call()` (deferred registration) |
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` (open/close animation) |
| `UnityEngine.UI` | `CanvasScaler` (scale factor for drag) |
| `Sirenix.OdinInspector` (optional) | `DisplayAsString`, `HideReferenceObjectPicker` |

---

## Architecture

```
UserInterfacePreset : RecordPreset<UserInterfaceData>
  ├── uiType: UserInterfaceTypes
  └── conditions: UnityUserInterfaceCondition[]

UserInterface : MonoBehaviour (partial)
  ├── preset: string (GUID)
  ├── tweeners: TweenerHub[]
  ├── dragZone: UIDragHandler
  ├── wndContainer: RectTransform
  │
  ├── OnEnable() → Register()
  │   ├── App.OnStart += Init / TimeController.Call(Init)
  │   └── Init():
  │       ├── UIProvider.Register(preset) → data
  │       ├── data.Init() (conditions)
  │       ├── data.OnOpen += Open()
  │       ├── data.OnClose += Close()
  │       └── LoadDragOffset()
  │
  ├── OnDisable() → Unregister()
  │   ├── data.DeInit() (conditions)
  │   └── UIProvider.Unregister(preset)
  │
  ├── Open() → tweeners[].Forward()
  ├── Close() → tweeners[].Back()
  └── UserInterfaceExtDrag (partial)
      ├── LoadDragOffset() → wndContainer.anchoredPosition
      ├── CalcPosition(delta) → clamp by Screen/CanvasScaler
      └── SetDragOffset(x, y) → data.Offset

CallUIHandler : MonoBehaviour
  ├── uiId: string (GUID)
  ├── closeUI: bool
  └── OnClick() → UIProvider.Open/Close/Toggle

CallUIClose : MonoBehaviour
  └── userInterface: UserInterface → Close()

UIDragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
  ├── dragStateSwitcher: UIStateSwitcher
  ├── OnDrag(delta) → callback
  ├── OnPointerDown → switcher.On
  └── OnPointerUp → switcher.Off

UnityUserInterfaceCondition : UserInterfaceCondition
  └── [DisplayAsString] Name (for Inspector)

Built-in conditions:
  ├── AutoLoadCondition        → always Open, reopens on Close
  ├── CloseOnOpenAnyUICondition → Close if another Common UI is open
  ├── SaveLoadStartCondition   → Open during Save/Load, Close on Idle
  └── OrCondition              → composite: nested conditions with priority
```

### Registration and Lifecycle

1. `UserInterface.OnEnable()` subscribes to `App.OnStart`
2. On `App.OnStart` (or via `TimeController.Call` if already `Started`), `Init()` is called
3. `Init()` registers the preset in `UIProvider`, initializes conditions, subscribes to `OnOpen`/`OnClose`
4. Safe subscribe: if conditions immediately return `Open`, the UI opens right away
5. `OnDisable()` deinitializes conditions and unregisters

### Animation

`UserInterface` stores a `TweenerHub[]` array. On `Open()`, `Forward()` is called on each; on `Close()` — `Back()`. If `tweeners` is empty, open/close happens instantly.

### Dragging

`UserInterfaceExtDrag` (partial) handles drag via `UIDragHandler`:
- `CalcPosition()` computes the new position accounting for `CanvasScaler.scaleFactor`
- Position is clamped to screen bounds (`Screen.width`, `Screen.height`)
- Offset is stored in `UserInterfaceData.Offset` → `GetDataForSave()` → `"x;y"`

### Composite Condition OrCondition

`OrCondition` contains an array of nested `UnityUserInterfaceCondition[]` and two fields:
- `conditionPriority` — answer returned on first match
- `notCondition` — answer if no condition matched

During `Check()`, iterates nested conditions; the first returning `conditionPriority` determines the result.

---

## Contract

### Input

- `UserInterfacePreset` in `Database` — GUID, type, conditions array
- `UserInterface` on scene — preset reference (GUID), tweeners, drag components
- `CallUIHandler` / `CallUIClose` — reference to GUID or `UserInterface`

### Output

- Animated UI open/close via `TweenerHub`
- Automatic visibility management through declarative conditions
- Window position persistence via `UserInterfaceData.Offset`

### API

| Component | Field/Method | Description |
|-----------|-------------|-------------|
| `UserInterface` | `preset` | Preset GUID from Database |
| `UserInterface` | `tweeners` | TweenerHub array for animation |
| `UserInterface` | `dragZone` | UIDragHandler (optional) |
| `UserInterface` | `wndContainer` | RectTransform window container (for drag) |
| `CallUIHandler` | `uiId` | Interface GUID |
| `CallUIHandler` | `closeUI` | `true` — close, `false` — open |
| `UIDragHandler` | `OnDrag` | Callback with movement delta |

### Constraints

| Constraint | Reason |
|------------|--------|
| Registration requires `App.GetState() >= Starting` | Depends on App initialization |
| `UIDragHandler` requires `CanvasScaler` in hierarchy | Delta scaling for correct movement |
| Drag position is `(int, int)` | Integer pixels, saved as `"x;y"` |
| `UserInterface` is not subclassed | Differences are set via preset and conditions |
| `OrCondition` does not support nested `OrCondition` | Single-level composition |

---

## Usage

### Creating an Interface

1. Create `UserInterfacePreset`: `Create > Database > UserInterface Preset`
2. Set `UIType` (`Common`, `Panel`, `Overlay`, `Popup`)
3. Add conditions to the `Conditions` array (or leave empty for manual control)
4. On the scene, create an object with `UserInterface` component, set the preset GUID
5. Add `TweenerHub` for animation (optional)

### Manual Control via Button

```csharp
// CallUIHandler component on a button:
// uiId = "settings-menu-guid"
// closeUI = false → open

// Or programmatically:
UIProvider.Open("settings-menu-guid");
UIProvider.Close("settings-menu-guid");
```

### Interface with Dragging

1. Add `UIDragHandler` to the window title bar zone
2. In `UserInterface`, set `dragZone` and `wndContainer`
3. Ensure `CanvasScaler` exists in the hierarchy
4. Position is saved automatically via `SaveSystem`

### Creating a Condition

```csharp
[Serializable]
public sealed class MyCondition : UnityUserInterfaceCondition
{
    protected override void Run()
    {
        SomeSystem.OnEvent += RunCallback;
        RunCallback(); // required — immediate check
    }

    public override void DeInit()
    {
        SomeSystem.OnEvent -= RunCallback;
    }

    public override ConditionAnswer Check()
    {
        return SomeSystem.IsActive
            ? ConditionAnswer.Open
            : ConditionAnswer.Close;
    }
}
```

---

## Editor Tools

### Condition Code Generation

Path: `Assets > Create > Vortex Templates > UI Condition`

Generates a `UnityUserInterfaceCondition` template with scaffolded `Run()`, `DeInit()`, `Check()` methods. Requires manual implementation: logic, event subscriptions, `RunCallback()` call in `Run()`.

### Debug Mode

Configuration: `DebugSettings` asset → `uiLogs` toggle
Flag: `Settings.Data().UiDebugMode`
Logging: Register/Unregister in `UIProvider` when active

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `CanvasScaler` missing during drag | Drag disabled, `Debug.LogError` |
| `UserInterface.OnEnable` before `App.Started` | Subscribes to `App.OnStart`, registration deferred |
| `UserInterface.OnEnable` after `App.Started` | `TimeController.Call(Init)` — registration next frame |
| Empty `tweeners` array | Open/close without animation |
| `CallUIHandler` with invalid `uiId` | `Log.Print(Error)`, no-op |
| Drag goes beyond screen bounds | Position clamped to `(0, 0)` — `(Screen.width, Screen.height)` |
| `OrCondition` — all nested return `Idle` | Returns `notCondition` |
| Subscribing to `data.OnOpen` when `IsOpen == true` | Callback fires immediately (safe subscribe) |
