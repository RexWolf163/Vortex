# UI Misc

**Namespace:** `Vortex.Unity.UI.Misc`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

General-purpose utility UI components: advanced button, counters, animated slider, data container, helper MonoBehaviours.

---

## Components

### AdvancedButton

Extended button with click modes, visual states, and correct behaviour inside `ScrollRect`. Implements `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`.

Visual states (via `UIStateSwitcher`): Free, Hover, Pressed.

| Click Mode | Handler | Scroll-drag protection |
|---|---|---|
| `OnTap` | `OnPointerDown` (immediate response) | none (by design — fires before any movement) |
| `OnUpInBorders` | `OnPointerClick` (release on the same object) | ✅ automatic (Unity does not invoke `OnPointerClick` if the EventSystem classified the gesture as a drag) |
| `OnUpAnywhere` | `OnPointerUp` (any release on this object) | none (by contract — release anywhere) |
| `OnClick` | `OnPointerClick` + time check `< TimeForClickMs (200ms)` | ✅ automatic |

`OnUpInBorders` and `OnClick` go through Unity's canonical `IPointerClickHandler`: the EventSystem decides whether the gesture was a click or a drag based on `EventSystem.pixelDragThreshold`. When the button lives inside a `ScrollRect`, the parent receives the drag on scrolling and `OnPointerClick` is not invoked on the button — clicks are correctly suppressed without timers or custom thresholds.

If a custom distance threshold is needed, set it globally via `EventSystem.current.pixelDragThreshold` (a Unity setting that affects all of UGUI).

Events (Action): `OnClick`, `OnPressed`, `OnReleased`, `OnHover`, `OnExit`.
UnityEvents (arrays): `onClick[]`, `onHover[]`, `onExit[]`.
External control: `Press()`, `Release()`, `AddOnClick(UnityAction)`, `RemoveOnClick(UnityAction)`.

Notes:
- `OnPointerEnter` keeps the Pressed visual when the button is still held (scenario: pressed → cursor leaves → cursor returns).
- `AddOnClick(UnityAction)` is idempotent: a repeated subscription with the same `UnityAction` is ignored (via `_wrappedActions` dictionary); `RemoveOnClick` still detaches the wrapper.
- External `Press()`/`Release()` works in `OnClick` mode: with `eventData == null` the shift is treated as zero, and the time check still applies.

### DataStorage

Universal data container. Implements `IDataStorage : IDataSource`. FIFO search by type.

```csharp
storage.SetData(myModel);                    // full replacement of all data → OnUpdateLink
storage.SetData(new[] { a, b });             // full replacement with set     → OnUpdateLink
storage.AddData(extraData);                  // add/replace by type            → OnUpdateLink not fired
var model = storage.GetData<MyModel>();      // search by type
storage.OnUpdateLink += ReBindAll;           // re-bind all references
```

`OnUpdateLink` is link-level: fired only on full content replacement (`SetData`), which invalidates references previously obtained via `GetData<T>()`. `AddData` does not fire the event since existing references remain valid.

### DataCapturer

Late-binding bridge: `MonoBehaviour` source + reactive property name → `IDataStorage`. Implements `IDataStorage : IDataSource`. Configured in the inspector; in editor mode the property dropdown is built via reflection by filtering for `IReactiveData` (covers `IntData` / `BoolData` / `FloatData` / any `ReactiveValue<T>` descendant).

```csharp
// On the prefab:
//   source   = reference to any MonoBehaviour source
//   property = property name (picked via ValueSelector filtered by IReactiveData)

// Consumer (Pool item, generic handler, etc.):
capturer.OnUpdateLink += () =>
{
    var data = capturer.GetData<IntData>();  // reference to ReactiveValue
    data.OnUpdate += v => UpdateView(v);     // subscribe to value-level
};
```

Lifecycle:
- `Awake` — caches `PropertyInfo`. If the property is missing, logs the error, disables the component (`enabled = false`), and does not subscribe to the source.
- If the source implements `IDataSource`, subscribes to its `OnUpdateLink` to track recreation of reactive fields.
- `Start` — first `RefreshLink()` after all scene `Awake`s; consumers have time to subscribe.
- `RefreshLink` compares new and old references via `ReferenceEquals` and fires `OnUpdateLink` only on actual reference change.
- `OnDestroy` — unsubscribes from the source, nullifies cache.

Use case: bindings that cannot be hard-wired via `[UIComponentLink]` (attribute binding in widget code) — generic Pool items, template widgets, inspector-time wiring on prefabs. The source is always a `MonoBehaviour` — for `ScriptableObject` settings/presets use a direct `[SerializeField]` reference, not `DataCapturer`.

### CounterViewBase&lt;T&gt; (abstract)

Base component for a counter view with min/max/current value, slider, pulse animation, and threshold visual states.

Subclass implements:
- `int GetValue()` — current value
- `int GetMinValue()` — range minimum
- `int GetMaxValue()` — range maximum
- `void Init()` / `void DeInit()` — domain subscriptions

The data model is accessed via `protected T Data` (cached on first access, reset on `UpdateLink`).

Inspector fields (all optional — the component works with any subset):

| Group | Field | Description |
|---|---|---|
| Source | `sourceValue` | `IDataStorage` (via `ClassFilter` + `AutoLink`) — data model source |
| Min Value UI | `min` (UIComponent), `patternMin = "{0}"` | Min text widget and its format pattern |
| Max Value UI | `max` (UIComponent), `patternMax = "{0}"` | Max text widget |
| Current Value UI | `value` (UIComponent), `patternValue = "{2} < {0} < {1}"` | Current value text widget. Receives three args: `{0}` = value, `{1}` = max, `{2}` = min |
| — | `slider` (SliderView) | Animated slider |
| — | `tweenPulsation` (TweenerHub) | Pulse animation on value change |
| — | `switcher` (UIStateSwitcher over `CounterStates`) | Threshold visual states |
| Animations | `onUp` / `onDown` | Animate on increase / decrease |

`CounterStates` thresholds (based on fill percentage of the range):

| State | Condition |
|---|---|
| `Empty` | `value == minValue` |
| `Less20` | < 20% of range |
| `Less50` | < 50% |
| `Less80` | < 80% |
| `Less100` | ≥ 80% and < `maxValue` |
| `Fill` | `value == maxValue` |

Subclass API:
- `UpdateValue()` / `UpdateMinValue()` / `UpdateMaxValue()` — manually refresh the corresponding block.

### CounterViewAdvanced

A ready-to-use `CounterViewBase<T>` subclass for the typical "model with three `IntData` (current/min/max)" scenario. Holds `[SerializeField]` references to the source's reactive properties, maps them to `GetValue`/`GetMinValue`/`GetMaxValue`, and subscribes in `Init()`.

### SliderView

Animated Slider via `AsyncTween`.

```csharp
sliderView.Set(0.75f, 1f);   // value, max
```

| Field | Type | Description |
|-------|------|-------------|
| `slider` | `Slider` | Target slider |
| `delay` | `float` | Delay before animation (`[Range(0, 3)]`) |
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
| `Vortex.Core.System` | `IDataSource`, `IDataStorage`, `IReactiveData` |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `IntData`, `BoolData`, `FloatData` — `DataCapturer` property filter |
| `Vortex.Core.Extensions` | `ActionExt.Fire()` |
| Sirenix Odin Inspector | `ValueSelector`, `FoldoutGroup` — `DataCapturer` UX |
| TextMeshPro | TMP in `CounterView` |

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `AdvancedButton` in `OnClick`/`OnUpInBorders` mode inside a `ScrollRect` | When scrolling, `OnPointerClick` is not invoked — clicks are suppressed automatically |
| `AdvancedButton` in `OnUpAnywhere` mode inside a `ScrollRect` | Fires on any release (by the mode's contract, no drag protection) |
| `AdvancedButton.Press()` / `Release()` externally | Works without pointer events; in `OnClick` mode the shift is treated as zero and the time check is applied |
| `AdvancedButton.AddOnClick(action)` repeated with the same `action` | Ignored (idempotent); `RemoveOnClick` still detaches the wrapper |
| `AdvancedButton.OnPointerEnter` after exit with the button held | The visual returns to Pressed rather than Hover |
| `CounterViewBase.OnEnable` with an empty source | NRE on `StorageValue.OnUpdateLink += ...` (fail-fast) |
| `DataStorage.GetData<T>()` — type not found | Returns `null` |
| `DataStorage.AddData()` — addition | `OnUpdateLink` not fired (link-level not violated) |
| `DataCapturer` — property renamed or missing | `Debug.LogError` + `enabled = false`; `Start`/`RefreshLink` do not run |
| `DataCapturer` — source implements `IDataSource` | Subscribes to source `OnUpdateLink`, `RefreshLink` on signal |
| `DataCapturer.RefreshLink` — reference unchanged | `OnUpdateLink` not fired (`ReferenceEquals`) |
| `DataCapturer` — `source` or property not assigned in inspector | NRE in `Awake` (fail-fast by canon) |
| `SliderView.Set()` — same value/max | Update skipped |
| `EnableDelayForChild` — `OnDisable` before delay | Children deactivated, timer removed |
| `AutoRectSetter` in Editor | Updates on `OnValidate` |
