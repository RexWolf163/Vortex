# UI Misc

**Namespace:** `Vortex.Unity.UI.Misc`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

General-purpose utility UI components: advanced button, counters, animated slider, data container, helper MonoBehaviours.

---

## Components

### AdvancedButton

Extended button with click modes, visual states, and gesture tracking. Implements `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`.

Visual states (via `UIStateSwitcher`): Free, Hover, Pressed.

| Click Mode | Description |
|------------|-------------|
| `OnTap` | Fires immediately on press |
| `OnUpInBorders` | Fires on release within bounds |
| `OnUpAnywhere` | Fires on release anywhere |
| `OnClick` | Fires if press+release without movement > 20px |

Events (Action): `OnClick`, `OnPressed`, `OnReleased`, `OnHover`, `OnExit`.
UnityEvents (arrays): `onClick[]`, `onHover[]`, `onExit[]`.
External control: `Press()`, `Release()`, `AddOnClick(UnityAction)`, `RemoveOnClick(UnityAction)`.

### DataStorage

Universal data container. Implements `IDataStorage`. FIFO search by type.

```csharp
storage.SetData(myModel);                    // replace all data
storage.AddData(extraData);                  // add/replace by type
var model = storage.GetData<MyModel>();      // search by type
storage.OnUpdateLink += OnDataChanged;       // update event
```

### CounterView (abstract)

Abstract component for displaying numeric counters with change animation.

Subclass implements:
- `int GetValue()` — current value
- `int? GetMaxValue()` — maximum value (nullable)

Capabilities:
- Support for Text, TextMeshPro, TextMeshProUGUI (arrays)
- Separate arrays for max values
- Format patterns (`pattern`, `patternMax`)
- Tweener animation on increase (`onUp`) and/or decrease (`onDawn`)
- `SliderView` integration
- Value caching to prevent redundant updates
- `Refresh()` — manual update

### SliderView

Animated Slider via `AsyncTween`.

```csharp
sliderView.Set(0.75f, 1f);   // value, max
```

| Field | Type | Description |
|-------|------|-------------|
| `slider` | `Slider` | Target slider |
| `duration` | `float` | Animation duration (0..1 sec) |
| `ease` | `EaseType` | Easing type |

Skips update if value and max are unchanged.

### AutoRectSetter

Auto-configure RectTransform via Inspector. `[ExecuteAlways]` — works in both Editor and Play mode.

Configurable parameters (each enabled by toggle):
- Borders (left, top, right, bottom, posZ)
- Anchors (anchorMin, anchorMax)
- Pivot
- Rotation (localEulerAngles)

`Apply()` — apply settings. `ReadFromCurrent()` — capture current RectTransform values.

### EnableDelayForChild

Delayed child object activation.

| Field | Type | Description |
|-------|------|-------------|
| `delay` | `float` | Delay (0..10 sec) |

`Awake` — deactivates all children. `OnEnable` — schedules activation via `TimeController.Call()`. `OnDisable` — deactivates.

### ScrollRectResetHandler

Resets `ScrollRect` to initial position (`normalizedPosition = Vector2.one`) on `Start`.

### DropDown

Dropdown list component. Consists of four classes:

- `DropDownComponent` — controller: toggle open/close, configuration via `SetList(texts, callback, value)`. Supports sorting (`sorting`), `UnityEvent<int> onSelected`, `closeOnSelected`, `scrollSensitivity`. When sorting is enabled, builds forward (`_map`) and reverse (`_mapBack`) index maps between sorted and original order.
- `DropDownList` — Pool-based list, scroll-positions to selected element via `ScrollRect.normalizedPosition`. Caches text hash (`string.Join`) — on repeated `Set()` with same data, only updates `Current` without recreating the pool.
- `DropDownItem` — list element. Receives `DropDownListModel` and `IntData` (index) via `IDataStorage`. Visually highlights current element via `UIComponent.SetSwitcher(SwitcherState.On/Off)`. Subscribes to `OnUpdateData` for refresh.
- `DropDownListModel` — `IReactiveData` model: callbacks (select, close), texts, current selection, `closeOnSelected`, `ScrollSensitivity`. `Dispose()` clears subscribers.

API:
```csharp
dropDown.SetList(texts, OnSelect, currentValue);  // configuration
dropDown.SetValue(3);                              // programmatic switch
int idx = dropDown.GetValue();                     // original index
string text = dropDown.GetValueItem();             // selected text
```

`Select()` callback always returns the original (unsorted) index via `_mapBack`.

The list is instantiated into the `Canvas` on first open, deactivated on close, destroyed when the controller is destroyed.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` — `AdvancedButton` visual states |
| `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` | `AsyncTween`, `EaseType` — `SliderView` animation |
| `Vortex.Unity.AppSystem` | `TimeController` — deferred calls |
| `Vortex.Core.System` | `IDataStorage` — interface |
| `Vortex.Core.Extensions` | `ActionExt.Fire()` |
| TextMeshPro | TMP in `CounterView` |

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `AdvancedButton.OnClick` in `OnClick` mode — swipe | Does not fire (movement > 20px) |
| `AdvancedButton.Press()` / `Release()` externally | Works without pointer events |
| `DataStorage.GetData<T>()` — type not found | Returns `null` |
| `CounterView.Refresh()` — value unchanged | Update skipped (cache) |
| `SliderView.Set()` — same value/max | Update skipped |
| `EnableDelayForChild` — `OnDisable` before delay | Children deactivated, timer removed |
| `AutoRectSetter` in Editor | Updates on `OnValidate` |
